"""Apply Evolution API / Map Variables fix to exported n8n current workflow."""
import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parent
RAW = ROOT / "n8n-kuafor-automation-current-raw.json"
OUT = ROOT / "n8n-kuafor-automation-current-fixed.json"

EVOLUTION_SERVER_EXPR = (
    "={{ $('Evolution Webhook').first().json.body.server_url "
    "|| $('Evolution Webhook').first().json.server_url }}"
)
EVOLUTION_APIKEY_EXPR = (
    "={{ $('Evolution Webhook').first().json.body.apikey "
    "|| $('Evolution Webhook').first().json.apikey }}"
)
SEND_URL = (
    "={{ $('Map Variables').first().json.EvolutionServerUrl }}"
    "/message/sendText/{{ $('Map Variables').first().json.BusinessInstance }}"
)

AGGREGATE_CODE = r"""const root = $input.first().json;
const slots = root.slots ?? root.availableSlots ?? root.items ?? root;
const list = Array.isArray(slots) ? slots : (slots && typeof slots === 'object' ? [slots] : []);
return [{
  json: {
    slots: list,
    count: list.length,
    summary: list.length
      ? list.map(s => (typeof s === 'string' ? s : (s.time || s.start || s.label || JSON.stringify(s)))).join(', ')
      : 'Uygun slot bulunamadi.',
    raw: root,
  },
}];
"""


def patch(data: dict) -> dict:
    for node in data.get("nodes", []):
        name = node.get("name", "")

        if name == "Map Variables":
            assigns = node["parameters"]["assignments"]["assignments"]
            # .first() on existing webhook refs
            for a in assigns:
                if "Evolution Webhook').item." in a.get("value", ""):
                    a["value"] = a["value"].replace(
                        "Evolution Webhook').item.", "Evolution Webhook').first()."
                    )
            names = {a["name"] for a in assigns}
            if "EvolutionServerUrl" not in names:
                assigns.append(
                    {
                        "id": "v6",
                        "name": "EvolutionServerUrl",
                        "value": EVOLUTION_SERVER_EXPR,
                        "type": "string",
                    }
                )
            if "EvolutionApiKey" not in names:
                assigns.append(
                    {
                        "id": "v7",
                        "name": "EvolutionApiKey",
                        "value": EVOLUTION_APIKEY_EXPR,
                        "type": "string",
                    }
                )

        if name == "Send via Evolution API":
            p = node["parameters"]
            p["url"] = f"={SEND_URL}"
            for h in p.get("headerParameters", {}).get("parameters", []):
                if h.get("name") == "apikey":
                    h["value"] = "={{ $('Map Variables').first().json.EvolutionApiKey }}"
            for bp in p.get("bodyParameters", {}).get("parameters", []):
                if bp.get("name") == "text":
                    bp["value"] = "={{ $('AI Agent').first().json.output }}"
            node["executeOnce"] = True

        if name == "Müşteriye Özür (WhatsApp)":
            node["parameters"]["url"] = (
                "={{ $('Map Variables').first().json.EvolutionServerUrl }}"
                "/message/sendText/{{ $('Map Variables').first().json.BusinessInstance }}"
            )
            for h in node["parameters"].get("headerParameters", {}).get("parameters", []):
                if h.get("name") == "apikey":
                    h["value"] = "={{ $('Map Variables').first().json.EvolutionApiKey }}"

        # Tools: stable Mega Context after agent tool runs
        if name in ("kendi_sistemine_kaydet", "takvimi_oku", "randevu_sil"):
            for h in node["parameters"].get("headerParameters", {}).get("parameters", []):
                if "Get Mega Context').item." in h.get("value", ""):
                    h["value"] = h["value"].replace(
                        "Get Mega Context').item.", "Get Mega Context').first()."
                    )

        if name == "takvimi_oku":
            # Remove stale Google Calendar query params (API uses URL query from $fromAI)
            node["parameters"]["queryParameters"] = {"parameters": []}
            opts = node["parameters"].setdefault("options", {})
            opts.setdefault("response", {}).setdefault("response", {})["fullResponse"] = True

    # Optional: aggregate node after takvimi_oku is inline tool — document in README
    return data


def main() -> None:
    src = Path(sys.argv[1]) if len(sys.argv) > 1 else RAW
    if not src.exists():
        print(f"Missing {src}. Export workflow from n8n to this path first.")
        sys.exit(1)
    with src.open(encoding="utf-8") as f:
        data = json.load(f)
    if "name" not in data:
        data = {"name": "KuaforAutomation_Current", **data}
    patched = patch(data)
    with OUT.open("w", encoding="utf-8") as f:
        json.dump(patched, f, ensure_ascii=False, indent=2)
    print(f"Written: {OUT}")


if __name__ == "__main__":
    main()
