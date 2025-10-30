(() => {
    // exported API, register globally to enable calling it after
    const exports = window.FOMOcal = window.FOMOcal || {};

    exports.console = {
        hook: forward => {
            if (exports.console.hooked) return; // don't re-attach
            exports.console.hooked = true;

            [console.log, console.debug, console.info, console.warn, console.error].forEach(function (fn) {
                const level = fn.name || 'log'; // fallback to a generic existing log function
                if (typeof fn !== 'function') return; // nothing to hook

                console[level] = function (...args) { // overwrite original with hook
                    try {
                        const msg = args.map(a => typeof a === 'object' ? JSON.stringify(a) : String(a)).join(' ');
                        forward(level, msg);
                    } catch (err) {
                        console.error('Console hook failed:', err);
                    }

                    fn.apply(console, args); // call original console method
                };
            });
        }
    };
})();

console.debug('FOMOcal.console attached.');
