(function () {
    document.querySelectorAll('.otp-input').forEach(function (input, index) {
        input.addEventListener('keyup', function (e) {
            if (e.target.value.length === 1 && index < 5) {
                document.querySelectorAll('.otp-input')[index + 1].focus();
            }
        });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'Backspace' && e.target.value === '' && index > 0) {
                document.querySelectorAll('.otp-input')[index - 1].focus();
            }
        });
    });

    var currentPhone = '';
    var otpValiditySeconds = 90;

    /** Doğrulama: gizli alan → telefon input → bellek (E2E API OTP + panel uyumu). */
    function resolveLoginPhone() {
        var hidden = (document.getElementById('loginPhone') && document.getElementById('loginPhone').value) || '';
        hidden = String(hidden).trim();
        var fromField = (document.getElementById('phoneNumber') && document.getElementById('phoneNumber').value) || '';
        fromField = String(fromField).trim();
        var stored = String(currentPhone || '').trim();
        return hidden || fromField || stored;
    }

    function syncLoginPhoneHidden() {
        var phone = resolveLoginPhone();
        var hidden = document.getElementById('loginPhone');
        if (hidden) hidden.value = phone;
        if (phone) currentPhone = phone;
    }

    function syncCurrentPhoneFromInput() {
        syncLoginPhoneHidden();
    }

    var countdownInterval = null;

    function startCountdown(seconds) {
        clearInterval(countdownInterval);
        var remaining = seconds;
        document.getElementById('countdownTimer').textContent = remaining;
        document.getElementById('otpCountdownWrap').classList.remove('d-none');
        document.getElementById('resendWrap').classList.add('d-none');

        countdownInterval = setInterval(function () {
            remaining--;
            document.getElementById('countdownTimer').textContent = remaining;
            if (remaining <= 0) {
                clearInterval(countdownInterval);
                document.getElementById('otpCountdownWrap').classList.add('d-none');
                document.getElementById('resendWrap').classList.remove('d-none');
            }
        }, 1000);
    }

    async function resendOtp() {
        var errBox = document.getElementById('errorMessage');
        errBox.classList.add('d-none');
        document.getElementById('resendWrap').classList.add('d-none');

        try {
            syncLoginPhoneHidden();
            var response = await fetch('/Auth/GenerateOtp', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ phoneNumber: resolveLoginPhone() })
            });
            var data = await response.json();
            if (data.success) {
                startCountdown(otpValiditySeconds);
                document.querySelectorAll('.otp-input').forEach(function (i) { i.value = ''; });
                document.querySelector('.otp-input').focus();
            } else {
                showError(data.message || 'Kod tekrar gönderilemedi.');
                document.getElementById('resendWrap').classList.remove('d-none');
            }
        } catch {
            showError('Sunucu ile bağlantı kurulamadı.');
            document.getElementById('resendWrap').classList.remove('d-none');
        }
    }
    window.resendOtp = resendOtp;

    async function requestOtp(e) {
        e.preventDefault();
        var btn = document.getElementById('btnRequestOtp');
        var errBox = document.getElementById('errorMessage');

        errBox.classList.add('d-none');
        toggleButtonLoader(btn, true);

        syncLoginPhoneHidden();

        try {
            var response = await fetch('/Auth/GenerateOtp', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ phoneNumber: resolveLoginPhone() })
            });

            var data = await response.json();

            if (data.success) {
                document.getElementById('step1').classList.add('d-none');
                document.getElementById('step2').classList.remove('d-none');
                document.getElementById('stepDescription').innerText = 'Giriş işlemini tamamlamak için kodu doğrulayın.';
                syncLoginPhoneHidden();
                document.querySelector('.otp-input').focus();
                startCountdown(otpValiditySeconds);
            } else {
                showError(data.message || 'Mesaj gönderilemedi. Lütfen numarayı kontrol edin.');
                if (response.status === 429 || response.status === 403) {
                    btn.disabled = true;
                }
            }
        } catch {
            showError('Sunucu ile bağlantı kurulamadı.');
        } finally {
            toggleButtonLoader(btn, false);
        }
    }
    window.requestOtp = requestOtp;

    async function verifyOtp(e) {
        e.preventDefault();
        var btn = document.getElementById('btnVerifyOtp');
        var errBox = document.getElementById('errorMessage');

        errBox.classList.add('d-none');
        toggleButtonLoader(btn, true);

        var inputs = document.querySelectorAll('.otp-input');
        var otpCode = '';
        inputs.forEach(function (i) { otpCode += i.value; });

        try {
            var tokenInput = document.querySelector('#otpForm input[name="__RequestVerificationToken"]')
                || document.querySelector('input[name="__RequestVerificationToken"]');
            var token = tokenInput ? tokenInput.value : '';
            var response = await fetch('/Auth/VerifyOtp', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': token },
                body: JSON.stringify({ phoneNumber: resolveLoginPhone(), otpCode: otpCode })
            });

            var contentType = response.headers.get('content-type') || '';
            var data = null;
            if (contentType.includes('application/json')) {
                data = await response.json();
            } else {
                var raw = await response.text();
                try {
                    data = JSON.parse(raw);
                } catch {
                    showError(
                        response.status === 401
                            ? 'Hatalı veya süresi geçmiş kod.'
                            : 'Giriş tamamlanamadı. Lütfen tekrar deneyin.'
                    );
                    return;
                }
            }

            if (response.ok && data.success) {
                window.location.assign('/Dashboard/Index');
                return;
            }

            showError(data.message || 'Hatalı veya süresi dolmuş kod.');

            if (response.status === 429 || response.status === 403) {
                inputs.forEach(function (i) { i.disabled = true; });
                document.getElementById('otpCountdownWrap').classList.add('d-none');
                document.getElementById('resendWrap').classList.add('d-none');
                clearInterval(countdownInterval);

                document.getElementById('step2').classList.add('d-none');
                document.getElementById('step1').classList.remove('d-none');
                document.getElementById('stepDescription').innerText = 'Sisteme giriş yapmak için telefon numaranızı girin.';
                document.getElementById('phoneNumber').disabled = true;
                document.getElementById('btnRequestOtp').disabled = true;
            } else {
                inputs.forEach(function (i) { i.value = ''; });
                inputs[0].focus();
            }
        } catch {
            showError('Doğrulama sırasında bir hata oluştu.');
        } finally {
            toggleButtonLoader(btn, false);
        }
    }
    window.verifyOtp = verifyOtp;

    function backToStep1() {
        syncCurrentPhoneFromInput();
        document.getElementById('step2').classList.add('d-none');
        document.getElementById('step1').classList.remove('d-none');
        document.getElementById('stepDescription').innerText = 'Sisteme giriş yapmak için telefon numaranızı girin.';
        document.getElementById('errorMessage').classList.add('d-none');
        clearInterval(countdownInterval);

        document.querySelectorAll('.otp-input').forEach(function (i) {
            i.value = '';
            i.disabled = false;
        });
        document.getElementById('btnVerifyOtp').disabled = false;
        document.getElementById('btnRequestOtp').disabled = false;
        document.getElementById('otpCountdownWrap').classList.remove('d-none');
        document.getElementById('resendWrap').classList.add('d-none');
    }
    window.backToStep1 = backToStep1;

    function showError(msg) {
        var errBox = document.getElementById('errorMessage');
        errBox.querySelector('span').innerText = msg;
        errBox.classList.remove('d-none');
        errBox.classList.add('d-flex');
    }

    function toggleButtonLoader(btn, isLoading) {
        var text = btn.querySelector('.btn-text');
        var spinner = btn.querySelector('.spinner-border');

        if (isLoading) {
            text.classList.add('opacity-50');
            btn.classList.add('pe-none');
            spinner.classList.remove('d-none');
        } else {
            text.classList.remove('opacity-50');
            btn.classList.remove('pe-none');
            spinner.classList.add('d-none');
        }
    }
})();
