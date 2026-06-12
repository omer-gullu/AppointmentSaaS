(function () {
    const originalFetch = window.fetch;
    window.fetch = async function () {
        let [resource, config] = arguments;
        config = config || {};
        const method = (config.method || 'GET').toUpperCase();
        if (method === 'POST' || method === 'DELETE' || method === 'PUT' || method === 'PATCH') {
            let isSameOrigin = true;
            try {
                const target = new URL(resource, window.location.href);
                isSameOrigin = target.origin === window.location.origin;
            } catch {
                isSameOrigin = false;
            }
            if (isSameOrigin) {
                const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
                if (tokenInput?.value) {
                    const headers = new Headers(config.headers || {});
                    headers.set('RequestVerificationToken', tokenInput.value);
                    config.headers = headers;
                }
            }
        }
        return originalFetch(resource, config);
    };
})();
