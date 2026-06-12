(function () {
    var cfgEl = document.getElementById('change-plan-config');
    if (!cfgEl) return;

    var cfg = JSON.parse(cfgEl.textContent);
    var currentBilling = 'monthly';

    function setBilling(type) {
        currentBilling = type;
        document.getElementById('btnMonthly').classList.toggle('active', type === 'monthly');
        document.getElementById('btnYearly').classList.toggle('active', type === 'yearly');

        ['starter', 'pro'].forEach(function (p) {
            var d = cfg.priceData[type][p];
            var amtEl = document.getElementById('amt-' + p);
            if (amtEl) amtEl.innerHTML = d.amt + '<sub> ₺/ay</sub>';
            var noteEl = document.getElementById('ynote-' + p);
            if (noteEl) noteEl.innerHTML = d.note || '';

            var card = document.querySelector('[data-plan="' + p + '"]');
            var form = card && card.querySelector('.plan-change-form');
            var cycleInput = form && form.querySelector('.billing-cycle-input');
            var btn = form && form.querySelector('.plan-select-btn');
            if (!form || !cycleInput || !btn) return;

            var planType = form.dataset.planType;
            var cycle = type === 'yearly' ? cfg.billingYearly : cfg.billingMonthly;
            cycleInput.value = cycle;

            var isCurrent = !cfg.isTrial
                && planType.localeCompare(cfg.currentPlanType, undefined, { sensitivity: 'accent' }) === 0
                && cycle === cfg.currentBillingCycle;

            var enabled = cfg.canSelectPlan && !isCurrent;
            btn.disabled = !enabled;
            if (type === 'yearly' && enabled) {
                btn.textContent = 'Seç ve öde — ' + d.fullPrice + ' ₺/yıl';
            } else if (isCurrent) {
                btn.textContent = type === 'yearly' ? 'Mevcut Plan (Yıllık)' : 'Mevcut Plan (Aylık)';
            } else {
                btn.textContent = 'Seç ve öde';
            }
        });
    }
    window.setBilling = setBilling;

    var cancelForm = document.getElementById('cancelBeforePlanForm');
    if (cancelForm) {
        cancelForm.addEventListener('submit', function (e) {
            e.preventDefault();
            var form = this;
            var loadSwal = window.loadSweetAlert;
            if (!loadSwal) { form.submit(); return; }
            loadSwal().then(function (Swal) {
                Swal.fire({
                    title: 'Yenilemeyi durdur',
                    html: '<p class="text-muted mb-0">Otomatik yenileme iptal edilir; <strong>' + cfg.endDateText + '</strong> tarihine kadar mevcut planınızı kullanmaya devam edersiniz. Sonrasında yeni plan seçebilirsiniz.</p>',
                    icon: 'question',
                    showCancelButton: true,
                    confirmButtonColor: '#ef4444',
                    cancelButtonColor: '#6b7280',
                    confirmButtonText: 'Evet, iptal et',
                    cancelButtonText: 'Vazgeç'
                }).then(function (r) { if (r.isConfirmed) form.submit(); });
            });
        });
    }

    setBilling('monthly');
})();
