(function () {
    'use strict';

    function ready(fn) {
        if (document.readyState === 'loading') {
            document.addEventListener('DOMContentLoaded', fn);
        } else {
            fn();
        }
    }

    ready(function () {
        var root = document.getElementById('dashboard-page');
        if (!root) return;

        var waConnected = root.dataset.waConnected === 'true';
        var subscriptionEnd = root.dataset.subscriptionEnd || '';

        var waModalEl = document.getElementById('whatsAppModal');
        var editModalEl = document.getElementById('editAppointmentModal');
        var waModal = null;
        var editAppointmentModal = null;

        function getWaModal() {
            if (!waModalEl || typeof bootstrap === 'undefined') return null;
            if (!waModal) waModal = new bootstrap.Modal(waModalEl);
            return waModal;
        }

        function getEditModal() {
            if (!editModalEl || typeof bootstrap === 'undefined') return null;
            if (!editAppointmentModal) editAppointmentModal = new bootstrap.Modal(editModalEl);
            return editAppointmentModal;
        }

        function withSwal(fn) {
            if (window.Swal) {
                fn(window.Swal);
                return;
            }
            if (typeof window.loadSweetAlert === 'function') {
                window.loadSweetAlert().then(fn).catch(function () { });
            }
        }

        function swToast(icon, title) {
            withSwal(function (Swal) {
                Swal.fire({
                    toast: true,
                    position: 'top-end',
                    icon: icon,
                    title: title,
                    showConfirmButton: false,
                    timer: 3000,
                    timerProgressBar: true,
                    didOpen: function (toast) {
                        toast.addEventListener('mouseenter', Swal.stopTimer);
                        toast.addEventListener('mouseleave', Swal.resumeTimer);
                    }
                });
            });
        }

        function showTempDataError() {
            var el = document.getElementById('dashboard-temp-error');
            if (!el) return;
            var raw = el.textContent || '';
            if (!raw.trim()) return;

            withSwal(function (Swal) {
                try {
                    var errJson = JSON.parse(raw);
                    if (errJson.suggestedSlots && errJson.suggestedSlots.length > 0) {
                        Swal.fire({
                            icon: 'warning',
                            title: 'Seçilen saat dolu!',
                            html: 'Şu saatlere randevu oluşturabiliriz:<br><br><b>' + errJson.suggestedSlots.join(' - ') + '</b>',
                            confirmButtonText: 'Tamam',
                            confirmButtonColor: '#4f46e5'
                        });
                    } else {
                        Swal.fire('Hata!', errJson.message || errJson.Message || 'İşlem başarısız.', 'error');
                    }
                } catch (e) {
                    Swal.fire('Hata!', 'Sistem hatası oluştu.', 'error');
                }
            });
        }

        function openEditFromDataset(btn) {
            var modal = getEditModal();
            if (!modal) return;

            document.getElementById('editAppointmentId').value = btn.dataset.appointmentId || '';
            document.getElementById('editGoogleEventId').value = btn.dataset.googleEventId || '';
            document.getElementById('editCustomerName').value = btn.dataset.customerName || '';
            document.getElementById('editCustomerPhone').value = btn.dataset.customerPhone || '';
            document.getElementById('editServiceId').value = btn.dataset.serviceId || '';
            document.getElementById('editAppUserIdSelect').value = btn.dataset.appUserId || '';
            document.getElementById('editAppointmentDate').value = btn.dataset.date || '';
            document.getElementById('editAppointmentTime').value = btn.dataset.time || '';
            modal.show();
        }

        root.addEventListener('click', function (e) {
            var btn = e.target.closest('.btn-edit-appointment');
            if (!btn) return;
            e.preventDefault();
            openEditFromDataset(btn);
        });

        window.toggleBreakTimeFields = function () {
            var enabled = document.getElementById('breakTimeEnabled').checked;
            var fields = document.getElementById('breakTimeFields');
            if (!fields) return;
            fields.querySelectorAll('input[type="time"]').forEach(function (inp) {
                inp.disabled = !enabled;
            });
            fields.style.opacity = enabled ? '1' : '0.5';
        };

        window.saveBreakTime = function () {
            var payload = {
                isEnabled: document.getElementById('breakTimeEnabled').checked,
                startTime: document.getElementById('breakStartTime').value,
                endTime: document.getElementById('breakEndTime').value
            };

            var tokenInput = document.querySelector('#breakTimeForm input[name="__RequestVerificationToken"]')
                || document.querySelector('input[name="__RequestVerificationToken"]');
            var headers = { 'Content-Type': 'application/json' };
            if (tokenInput && tokenInput.value) {
                headers['RequestVerificationToken'] = tokenInput.value;
            }

            fetch('/Dashboard/UpdateBreakTime', {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(payload)
            })
                .then(function (res) { return res.json(); })
                .then(function (data) {
                    if (data.success) {
                        swToast('success', data.message || 'Mola saatleri güncellendi.');
                        setTimeout(function () { location.reload(); }, 1500);
                    } else {
                        swToast('error', data.message || 'Bir hata oluştu.');
                    }
                })
                .catch(function (err) { swToast('error', 'Bağlantı hatası: ' + err); });
        };

        if (document.getElementById('breakTimeEnabled')) {
            toggleBreakTimeFields();
        }

        window.toggleBhRow = function (dayIndex) {
            var row = document.querySelector('.bh-row[data-day="' + dayIndex + '"]');
            if (!row) return;
            var isChecked = row.querySelector('.bh-status').checked;
            row.querySelector('.bh-open').disabled = !isChecked;
            row.querySelector('.bh-close').disabled = !isChecked;
        };

        window.saveBusinessHours = function () {
            var hours = [];
            document.querySelectorAll('.bh-row').forEach(function (row) {
                var day = parseInt(row.getAttribute('data-day'), 10);
                var isClosed = !row.querySelector('.bh-status').checked;
                hours.push({
                    dayOfWeek: day,
                    isClosed: isClosed,
                    openTime: row.querySelector('.bh-open').value,
                    closeTime: row.querySelector('.bh-close').value
                });
            });

            fetch('/Dashboard/UpdateBusinessHours', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify(hours)
            })
                .then(function (res) { return res.json(); })
                .then(function (data) {
                    if (data.success) {
                        swToast('success', data.message || 'Çalışma saatleri güncellendi.');
                        setTimeout(function () { location.reload(); }, 1500);
                    } else {
                        swToast('error', data.message || 'Bir hata oluştu.');
                    }
                })
                .catch(function (err) { swToast('error', 'Bağlantı hatası: ' + err); });
        };

        window.loadWhatsAppQr = function () {
            var modal = getWaModal();
            if (!modal) return;
            var qrLoading = document.getElementById('qrLoading');
            var qrContainer = document.getElementById('qrContainer');
            var qrError = document.getElementById('qrError');
            var qrImage = document.getElementById('qrImage');

            qrLoading.style.display = 'block';
            qrContainer.style.display = 'none';
            qrError.style.display = 'block';
            document.getElementById('qrErrorMessage').innerText = 'Instance hazırlanıyor. QR kod için 5 saniye bekleniyor...';
            modal.show();

            fetch('/Dashboard/GetWhatsAppQr')
                .then(function (res) { return res.json(); })
                .then(function (data) {
                    qrLoading.style.display = 'none';
                    if (data.success) {
                        qrError.style.display = 'none';
                        qrImage.src = data.qrCode;
                        document.getElementById('modalInstanceName').innerText = data.instanceName;
                        qrContainer.style.display = 'block';
                    } else {
                        qrError.style.display = 'block';
                        document.getElementById('qrErrorMessage').innerText = data.message;
                    }
                })
                .catch(function () {
                    qrLoading.style.display = 'none';
                    qrError.style.display = 'block';
                    document.getElementById('qrErrorMessage').innerText = 'Sistemle bağlantı kurulamadı.';
                });
        };

        window.openEditAppointmentModal = function (id, customerName, customerPhone, serviceId, date, time, googleEventId, appUserId) {
            var modal = getEditModal();
            if (!modal) return;
            document.getElementById('editAppointmentId').value = id;
            document.getElementById('editGoogleEventId').value = googleEventId || '';
            document.getElementById('editCustomerName').value = customerName;
            document.getElementById('editCustomerPhone').value = customerPhone;
            document.getElementById('editServiceId').value = serviceId;
            document.getElementById('editAppUserIdSelect').value = appUserId || '';
            document.getElementById('editAppointmentDate').value = date;
            document.getElementById('editAppointmentTime').value = time;
            modal.show();
        };

        window.validateCreateAppointment = function () {
            var name = document.getElementById('appCustomerName').value.trim();
            var phone = document.getElementById('appCustomerPhone').value.trim();
            var staffId = document.getElementById('appUserId').value;

            if (!/^[a-zA-ZğüşıöçĞÜŞİÖÇ\s]+$/.test(name)) {
                withSwal(function (Swal) { Swal.fire('Hata!', 'Müşteri adı sadece harflerden oluşmalıdır.', 'warning'); });
                return false;
            }
            var numericPhone = phone.replace(/[^0-9]/g, '');
            if (numericPhone.length < 10 || numericPhone.length > 11) {
                withSwal(function (Swal) { Swal.fire('Hata!', 'Lütfen geçerli bir telefon numarası giriniz.', 'warning'); });
                return false;
            }
            if (!staffId) {
                withSwal(function (Swal) { Swal.fire('Hata!', 'Lütfen bir personel seçiniz.', 'warning'); });
                return false;
            }
            return true;
        };

        document.querySelectorAll('.delete-appointment-form').forEach(function (form) {
            form.addEventListener('submit', function (e) {
                e.preventDefault();
                var self = this;
                withSwal(function (Swal) {
                    Swal.fire({
                        title: 'Randevuyu Sil',
                        text: 'Bu randevuyu silmek istediğinize emin misiniz?',
                        icon: 'warning',
                        showCancelButton: true,
                        confirmButtonColor: '#ef4444',
                        cancelButtonColor: '#6b7280',
                        confirmButtonText: 'Evet, Sil',
                        cancelButtonText: 'İptal'
                    }).then(function (r) {
                        if (r.isConfirmed) self.submit();
                    });
                });
            });
        });

        var cancelForm = document.getElementById('cancelSubscriptionForm');
        if (cancelForm) {
            cancelForm.addEventListener('submit', function (e) {
                e.preventDefault();
                var self = this;
                withSwal(function (Swal) {
                    Swal.fire({
                        title: 'Aboneliği İptal Et',
                        html: '<p class="text-muted">Yenileme iptal edilir; ödenmiş döneminizin sonuna kadar (<strong>' + subscriptionEnd + '</strong>) kullanmaya devam edersiniz. Bu tarihten sonra hesap askıya alınır.</p>',
                        icon: 'warning',
                        showCancelButton: true,
                        confirmButtonColor: '#ef4444',
                        cancelButtonColor: '#6b7280',
                        confirmButtonText: 'Yenilemeyi İptal Et',
                        cancelButtonText: 'Vazgeç'
                    }).then(function (r) {
                        if (r.isConfirmed) self.submit();
                    });
                });
            });
        }

        var aiStatus = document.getElementById('aiStatus');
        if (aiStatus) {
            aiStatus.addEventListener('change', function () {
                var toggle = this;
                var isActive = toggle.checked;

                fetch('/Dashboard/ToggleAssistant', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                    body: 'isActive=' + isActive
                })
                    .then(function (res) { return res.json(); })
                    .then(function (data) {
                        if (!data.success) {
                            toggle.checked = !isActive;
                            swToast('error', 'AI asistan durumu güncellenemedi.');
                            return;
                        }
                        var isWa = data.isWhatsAppConnected !== undefined ? data.isWhatsAppConnected : waConnected;
                        document.getElementById('aiStatusText').innerText = isActive ? '7/24 Aktif' : 'Devre Dışı';
                        var badge = document.getElementById('systemBadge');
                        if (badge) {
                            if (!isActive) {
                                badge.style.background = '#f1f5f9';
                                badge.style.color = '#94a3b8';
                                badge.textContent = 'Pasif';
                            } else if (isWa) {
                                badge.style.background = '#dcfce7';
                                badge.style.color = '#16a34a';
                                badge.textContent = 'Hazır ✅';
                            } else {
                                badge.style.background = '#fef9c3';
                                badge.style.color = '#b45309';
                                badge.textContent = '⚠️ Bekliyor';
                            }
                        }
                        document.querySelectorAll('#aiWarningBox').forEach(function (box) {
                            if (isActive && !isWa) {
                                box.classList.remove('d-none');
                                var waCard = document.getElementById('whatsappCard');
                                if (waCard) {
                                    waCard.classList.add('pulse-warning');
                                    setTimeout(function () { waCard.scrollIntoView({ behavior: 'smooth', block: 'center' }); }, 350);
                                    setTimeout(function () { waCard.classList.remove('pulse-warning'); }, 6000);
                                }
                            } else {
                                box.classList.add('d-none');
                                document.getElementById('whatsappCard')?.classList.remove('pulse-warning');
                            }
                        });
                        var toastMsg = isActive && !isWa
                            ? "AI Asistan aktifleşti — lütfen WhatsApp'ı bağlayın."
                            : (isActive ? 'AI Asistan aktifleştirildi.' : 'AI Asistan devre dışı bırakıldı.');
                        swToast(isActive && !isWa ? 'warning' : 'success', toastMsg);
                    })
                    .catch(function () {
                        toggle.checked = !isActive;
                        swToast('error', 'Sistemle bağlantı kurulamadı.');
                    });
            });
        }

        showTempDataError();
    });
})();
