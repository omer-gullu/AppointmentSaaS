(function () {
    function formatCard(input) {
        var v = input.value.replace(/\D/g, '').substring(0, 16);
        input.value = v.replace(/(.{4})/g, '$1 ').trim();
    }
    window.formatCard = formatCard;

    window.validateRegisterForm = async function () {
        var loadSwal = window.loadSweetAlert;
        if (!loadSwal) return true;

        var name = document.getElementById('fieldFullName').value.trim();
        if (!/^[\p{L}\s'-]{3,}$/u.test(name)) {
            var Swal = await loadSwal();
            await Swal.fire({ icon: 'warning', title: 'Ad Soyad', text: 'Ad soyad alanı en az 3 karakter olmalı ve yalnızca harf içermelidir.' });
            return false;
        }

        var phoneRaw = document.getElementById('fieldPhone').value.replace(/\D/g, '');
        if (!/^[0-9]{11}$/.test(phoneRaw)) {
            var Swal2 = await loadSwal();
            await Swal2.fire({ icon: 'error', title: 'Geçersiz Telefon Numarası', text: 'Telefon numarası tam 11 haneli olmalıdır. (Örn: 05551234567)' });
            return false;
        }

        var email = document.querySelector('[name="UserEmail"]').value.trim();
        var emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]{2,}$/;
        if (!emailRegex.test(email)) {
            var Swal3 = await loadSwal();
            await Swal3.fire({ icon: 'error', title: 'Geçersiz E-posta', text: 'Lütfen geçerli bir e-posta adresi giriniz. (Örn: isim@firma.com)' });
            return false;
        }

        var biz = document.getElementById('fieldBusiness').value.trim();
        if (biz.length < 2) {
            var Swal4 = await loadSwal();
            await Swal4.fire({ icon: 'warning', title: 'İşletme Adı', text: 'Lütfen geçerli bir işletme adı giriniz.' });
            return false;
        }

        var sector = document.getElementById('fieldSector').value;
        if (!sector) {
            var Swal5 = await loadSwal();
            await Swal5.fire({ icon: 'warning', title: 'Sektör', text: 'Lütfen bir sektör seçiniz.' });
            return false;
        }

        var tc = document.getElementById('fieldIdentity').value.replace(/\D/g, '');
        document.getElementById('fieldIdentity').value = tc;
        var year = parseInt(document.getElementById('fieldBirthYear').value.trim(), 10);
        if (!/^[0-9]{10,11}$/.test(tc)) {
            var Swal6 = await loadSwal();
            await Swal6.fire({ icon: 'warning', title: 'T.C. Kimlik No', text: 'Lütfen 10 veya 11 haneli kimlik/vergi numaranızı giriniz.' });
            return false;
        }
        if (typeof window.isValidTcOrVkn === 'function' && !window.isValidTcOrVkn(tc)) {
            var SwalTc = await loadSwal();
            await SwalTc.fire({
                icon: 'warning',
                title: 'T.C. Kimlik / Vergi No',
                text: 'Girdiğiniz kimlik veya vergi numarası geçersiz (kontrol basamağı hatası).'
            });
            return false;
        }
        if (!year || year < 1900 || year > new Date().getFullYear() - 18) {
            var Swal7 = await loadSwal();
            await Swal7.fire({ icon: 'warning', title: 'Doğum Yılı', text: '18 yaşından büyük olmalısınız. Doğum yılını kimliğinizdeki gibi giriniz.' });
            return false;
        }

        document.getElementById('btnTxt').classList.add('d-none');
        document.getElementById('btnSpin').classList.remove('d-none');
        document.getElementById('btnSubmit').disabled = true;
        return true;
    };

    window.handleRegisterSubmit = async function (e) {
        e.preventDefault();
        var ok = await window.validateRegisterForm();
        if (ok) e.target.submit();
        return false;
    };
})();
