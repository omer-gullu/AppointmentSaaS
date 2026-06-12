"""
Patch n8n WhatsApp workflows: Evolution server_url/apikey via Map Variables,
Send via Evolution API uses .first(), executeOnce, calendar tool aggregate.
"""
from __future__ import annotations

import json
import re
import sys
from copy import deepcopy
from pathlib import Path

EVOLUTION_SERVER_EXPR = (
    "={{ $('Evolution Webhook').first().json.body.server_url "
    "|| $('Evolution Webhook').first().json.server_url "
    "|| $env.EVOLUTION_API_URL }}"
)

EVOLUTION_APIKEY_EXPR = (
    "={{ $('Evolution Webhook').first().json.body.apikey "
    "|| $('Evolution Webhook').first().json.apikey "
    "|| $env.EVOLUTION_API_KEY }}"
)

SEND_URL_EXPR = (
    "={{ $('Map Variables').first().json.EvolutionServerUrl }}"
    "/message/sendText/{{ $('Map Variables').first().json.BusinessInstance }}"
)

SEND_APIKEY_EXPR = "={{ $('Map Variables').first().json.EvolutionApiKey }}"

# Broken patterns from live n8n (webhook .item + body.*)
BROKEN_URL_PATTERNS = [
    r"\{\{\s*\$\('Evolution Webhook'\)\.item\.json\.body\.server_url\s*\}\}",
    r"\{\{\s*\$\('Evolution Webhook'\)\.first\(\)\.json\.body\.server_url\s*\}\}",
]

BROKEN_APIKEY_PATTERNS = [
    r"\{\{\s*\$\('Evolution Webhook'\)\.item\.json\.body\.apikey\s*\}\}",
    r"\{\{\s*\$\('Evolution Webhook'\)\.first\(\)\.json\.body\.apikey\s*\}\}",
]

CALENDAR_TOOL_NAMES = {
    "takvimi_oku",
    "Takvim Mevcut Randevuları Listele",
    "takvimi_oku_aggregate",
}

AGGREGATE_CODE = r"""// Tek item: Google Calendar events listesi
const root = $input.first().json;
const events = root.items ?? (Array.isArray(root) ? root : [root]);
return [{
  json: {
    appointments: events,
    count: events.length,
    summary: events.length
      ? events.map(e => {
          const start = e.start?.dateTime || e.start?.date || '';
          return (e.summary || 'Etkinlik') + ' @ ' + start;
        }).join('; ')
      : 'Takvimde kayitli randevu yok.',
  },
}];
"""


def _ensure_map_variables(node: dict) -> bool:
    assignments = node.setdefault("parameters", {}).setdefault("assignments", {}).setdefault(
        "assignments", []
    )
    names = {a.get("name") for a in assignments}
    changed = False
    new_fields = [
        ("EvolutionServerUrl", EVOLUTION_SERVER_EXPR, "string"),
        ("EvolutionApiKey", EVOLUTION_APIKEY_EXPR, "string"),
    ]
    for name, value, typ in new_fields:
        if name in names:
            for a in assignments:
                if a.get("name") == name and a.get("value") != value:
                    a["value"] = value
                    changed = True
        else:
            assignments.append(
                {
                    "id": f"evo-{name.lower()}",
                    "name": name,
                    "value": value,
                    "type": typ,
                }
            )
            changed = True

    for a in assignments:
        val = a.get("value", "")
        if "Evolution Webhook').item." in val:
            a["value"] = val.replace("Evolution Webhook').item.", "Evolution Webhook').first().")
            changed = True
    return changed


def _patch_send_node(node: dict) -> bool:
    params = node.setdefault("parameters", {})
    changed = False

    url = params.get("url", "")
    if isinstance(url, str):
        if "Evolution Webhook" in url or "$env.EVOLUTION_API_URL" in url:
            if url != f"={SEND_URL_EXPR}":
                params["url"] = f"={SEND_URL_EXPR}"
                changed = True
        for pat in BROKEN_URL_PATTERNS:
            if re.search(pat, url):
                params["url"] = f"={SEND_URL_EXPR}"
                changed = True

    headers = params.get("headerParameters", {}).get("parameters", [])
    for h in headers:
        if h.get("name") == "apikey":
            if h.get("value") != SEND_APIKEY_EXPR:
                h["value"] = SEND_APIKEY_EXPR
                changed = True

    body_params = params.get("bodyParameters", {}).get("parameters", [])
    for p in body_params:
        if p.get("name") == "text" and "AI Agent').item." in p.get("value", ""):
            p["value"] = "={{ $('AI Agent').first().json.output }}"
            changed = True

    if node.get("executeOnce") is not True:
        node["executeOnce"] = True
        changed = True
    return changed


def _replace_broken_strings(obj, key_path: str = "") -> bool:
    """Recursively fix broken Evolution Webhook references in any node."""
    changed = False
    if isinstance(obj, dict):
        for k, v in obj.items():
            if isinstance(v, str):
                new_v = v
                for pat in BROKEN_URL_PATTERNS:
                    new_v = re.sub(
                        pat,
                        "{{ $('Map Variables').first().json.EvolutionServerUrl }}",
                        new_v,
                    )
                for pat in BROKEN_APIKEY_PATTERNS:
                    new_v = re.sub(
                        pat,
                        "{{ $('Map Variables').first().json.EvolutionApiKey }}",
                        new_v,
                    )
                if (
                    "Evolution Webhook').item.json.body.server_url" in new_v
                    and "Map Variables" not in new_v
                ):
                    new_v = new_v.replace(
                        "$('Evolution Webhook').item.json.body.server_url",
                        "$('Map Variables').first().json.EvolutionServerUrl",
                    )
                if (
                    "Evolution Webhook').item.json.body.apikey" in new_v
                    and "Map Variables" not in new_v
                ):
                    new_v = new_v.replace(
                        "$('Evolution Webhook').item.json.body.apikey",
                        "$('Map Variables').first().json.EvolutionApiKey",
                    )
                if new_v != v:
                    obj[k] = new_v
                    changed = True
            else:
                if _replace_broken_strings(v, f"{key_path}.{k}"):
                    changed = True
    elif isinstance(obj, list):
        for i, item in enumerate(obj):
            if _replace_broken_strings(item, f"{key_path}[{i}]"):
                changed = True
    return changed


def _add_aggregate_after_calendar(nodes: list[dict], connections: dict) -> bool:
    """Insert Code aggregate node after calendar HTTP tool sub-workflows if present."""
    changed = False
    for node in nodes:
        name = node.get("name", "")
        if name not in CALENDAR_TOOL_NAMES:
            continue
        if node.get("type") == "n8n-nodes-base.code":
            continue
        # httpRequestTool: enable full response in one item (n8n keeps single item for JSON)
        opts = node.setdefault("parameters", {}).setdefault("options", {})
        if opts.get("response", {}).get("response", {}).get("fullResponse") is not True:
            opts.setdefault("response", {}).setdefault("response", {})["fullResponse"] = True
            changed = True
    return changed


def patch_workflow(data: dict) -> tuple[dict, list[str]]:
    data = deepcopy(data)
    log: list[str] = []
    nodes = data.get("nodes", [])

    for node in nodes:
        name = node.get("name", "")
        if name == "Map Variables":
            if _ensure_map_variables(node):
                log.append("Map Variables: EvolutionServerUrl + EvolutionApiKey (.first() fallbacks)")
        if name == "Send via Evolution API":
            if _patch_send_node(node):
                log.append("Send via Evolution API: Map Variables URL/apikey, executeOnce, AI .first()")

    if _replace_broken_strings(data):
        log.append("Replaced broken Evolution Webhook .item body.server_url/apikey references")

    if _add_aggregate_after_calendar(nodes, data.get("connections", {})):
        log.append("Calendar tool(s): fullResponse option for single-item JSON")

    return data, log


def process_file(path: Path, out_path: Path | None = None) -> None:
    if not path.exists():
        print(f"SKIP (not found): {path}")
        return
    with path.open(encoding="utf-8") as f:
        data = json.load(f)
    patched, log = patch_workflow(data)
    dest = out_path or path
    with dest.open("w", encoding="utf-8") as f:
        json.dump(patched, f, ensure_ascii=False, indent=2)
    print(f"OK {dest}")
    for line in log:
        print(f"  - {line}")
    if not log:
        print("  (no changes needed)")


def main() -> None:
    base = Path(__file__).resolve().parent
    desktop_auto = base.parent / "otomasyon yazılımı"
    targets = [
        desktop_auto / "KuaforAutomation_Pro.json",
        desktop_auto / "KuaforAutomation_MegaContext_fixed.json",
        desktop_auto / "KuaforAutomation (1).json",
        base / "n8n-kuafor-automation-main.json",
    ]
    out_main = base / "n8n-kuafor-automation-main.json"
    pro = desktop_auto / "KuaforAutomation_Pro.json"
    if pro.exists():
        with pro.open(encoding="utf-8") as f:
            data = json.load(f)
        patched, _ = patch_workflow(data)
        with out_main.open("w", encoding="utf-8") as f:
            json.dump(patched, f, ensure_ascii=False, indent=2)
        print(f"OK workspace export: {out_main}")

    for t in targets:
        if t.resolve() == out_main.resolve():
            continue
        process_file(t)

    # Aggregate tool sub-workflow (optional import)
    aggregate_path = base / "n8n-takvimi-oku-aggregate-tool-workflow.json"
    if not aggregate_path.exists():
        _write_aggregate_tool_workflow(aggregate_path)
        print(f"Created {aggregate_path}")


def _write_aggregate_tool_workflow(path: Path) -> None:
    workflow = {
        "name": "Takvim Oku Aggregate Tool",
        "nodes": [
            {
                "parameters": {},
                "id": "trigger",
                "name": "Execute Workflow Trigger",
                "type": "n8n-nodes-base.executeWorkflowTrigger",
                "typeVersion": 1.1,
                "position": [0, 0],
            },
            {
                "parameters": {
                    "jsCode": AGGREGATE_CODE,
                },
                "id": "aggregate",
                "name": "Aggregate Calendar Events",
                "type": "n8n-nodes-base.code",
                "typeVersion": 2,
                "position": [280, 0],
            },
        ],
        "connections": {
            "Execute Workflow Trigger": {
                "main": [[{"node": "Aggregate Calendar Events", "type": "main", "index": 0}]]
            }
        },
        "settings": {"executionOrder": "v1"},
        "meta": {"templateCredsSetupCompleted": True},
    }
    with path.open("w", encoding="utf-8") as f:
        json.dump(workflow, f, ensure_ascii=False, indent=2)


if __name__ == "__main__":
    main()
