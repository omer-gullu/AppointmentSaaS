(function () {
    var swalPromise;

    window.loadSweetAlert = function () {
        if (window.Swal) return Promise.resolve(window.Swal);
        if (swalPromise) return swalPromise;

        swalPromise = new Promise(function (resolve, reject) {
            var s = document.createElement('script');
            s.src = 'https://cdn.jsdelivr.net/npm/sweetalert2@11';
            s.defer = true;
            s.onload = function () { resolve(window.Swal); };
            s.onerror = reject;
            document.head.appendChild(s);
        });

        return swalPromise;
    };

    function preloadWhenIdle() {
        var run = function () { window.loadSweetAlert().catch(function () { }); };
        if ('requestIdleCallback' in window) {
            requestIdleCallback(run, { timeout: 4000 });
        } else {
            setTimeout(run, 2500);
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', preloadWhenIdle);
    } else {
        preloadWhenIdle();
    }
})();
