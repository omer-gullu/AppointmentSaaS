(function () {
    const cfgEl = document.getElementById('pricing-config');
    if (!cfgEl) return;

    const cfg = JSON.parse(cfgEl.textContent);
    let currentBilling = 'monthly';

    function setBilling(type) {
        currentBilling = type;
        const btnMonthly = document.getElementById('btnMonthly');
        const btnYearly = document.getElementById('btnYearly');
        if (btnMonthly) btnMonthly.classList.toggle('active', type === 'monthly');
        if (btnYearly) btnYearly.classList.toggle('active', type === 'yearly');

        ['starter', 'pro'].forEach(function (p) {
            const d = cfg.priceData[type][p];
            if (!d) return;
            const amtEl = document.getElementById('amt-' + p);
            if (amtEl) amtEl.innerHTML = d.amt + '<sub> ₺/ay</sub>';
            const noteEl = document.getElementById('ynote-' + p);
            if (noteEl) noteEl.innerHTML = d.note;
            const linkEl = document.getElementById('link-' + p);
            if (linkEl) linkEl.href = cfg.baseUrl + '?plan=' + p + '&cycle=' + type;
        });
    }

    setBilling('monthly');
    window.setBilling = setBilling;
})();
