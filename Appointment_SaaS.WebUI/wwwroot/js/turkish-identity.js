(function (global) {
    'use strict';

    function isAllDigits(value) {
        return /^[0-9]+$/.test(value);
    }

    function isValidTcKimlik(tc) {
        if (tc.length !== 11 || !isAllDigits(tc) || tc.charAt(0) === '0') return false;

        var d = tc.split('').map(function (c) { return parseInt(c, 10); });
        var sumOdd = d[0] + d[2] + d[4] + d[6] + d[8];
        var sumEven = d[1] + d[3] + d[5] + d[7];
        var digit10 = ((sumOdd * 7) - sumEven) % 10;
        if (digit10 < 0) digit10 += 10;
        var digit11 = d.slice(0, 10).reduce(function (a, b) { return a + b; }, 0) % 10;
        return d[9] === digit10 && d[10] === digit11;
    }

    function isValidVkn(vkn) {
        if (vkn.length !== 10 || !isAllDigits(vkn)) return false;

        var digits = vkn.split('').map(function (c) { return parseInt(c, 10); });
        var sum = 0;
        for (var i = 0; i < 9; i++) {
            var tmp = (digits[i] + (9 - i)) % 10;
            if (tmp !== 0) {
                tmp = (tmp * Math.pow(2, 9 - i)) % 9;
                if (tmp === 0) tmp = 9;
            }
            sum += tmp;
        }
        var lastDigit = (10 - (sum % 10)) % 10;
        return digits[9] === lastDigit;
    }

    function isValidTcOrVkn(value) {
        if (!value) return false;
        if (value.length === 11) return isValidTcKimlik(value);
        if (value.length === 10) return isValidVkn(value);
        return false;
    }

    global.isValidTcOrVkn = isValidTcOrVkn;
})(typeof window !== 'undefined' ? window : globalThis);
