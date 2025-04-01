(() => {
    const settings = {
        selector: null,
        intervalDelayMs: 200,
        maxMatches: 100,
        maxTries: 25
    };

    const triggerScroll = () => { dispatchEvent(new Event('scroll')); },
        getMatches = () => document.querySelectorAll(settings.selector),
        getMatchCount = () => getMatches().length,
        withOptions = options => { Object.assign(settings, options); };

    let notifyFound;

    function scrollDown() {
        /*  scroll to the bottom, delay to allow scroll position to update,
            then dispatch scroll event to trigger attached JS handlers */
        scrollTo({ top: document.body.scrollHeight, behavior: 'smooth' });
        setTimeout(triggerScroll, 500);

        // if the page itself doesn't scroll, maybe a container does
        document.querySelectorAll('*').forEach(el => {
            // if element is scrollable, scroll it to the bottom
            if (el.scrollHeight > el.clientHeight) el.scrollTop = el.scrollHeight;
        });
    }

    function start(stopIf) {
        let tries = 0,
            loading = 0; // running AJAX requests

        // helps count active AJAX requests
        const observer = new PerformanceObserver(list => {
            list.getEntries().forEach(entry => {
                if (entry.initiatorType === 'xmlhttprequest' || entry.initiatorType === 'fetch') {
                    loading++; // count up running requests
                    console.debug('loading', loading);

                    setTimeout(() => {
                        loading--; // make sure to count down again because we don't know when they end
                        console.debug('loading probably finished', loading);
                    }, 3000); // trying to give slow requests enough time before counting them out
                }
            });
        });

        observer.observe({ entryTypes: ['resource'] });

        const stopTrying = succeeded => {
            clearInterval(intervalID);
            observer.disconnect(); // to stop observing & clean up
            notifyFound(succeeded);
        };

        const intervalID = setInterval(() => {
            if (tries >= settings.maxTries) stopTrying(false); // stop trying when maxTries are exceeded

            const found = getMatchCount();
            console.debug('checking for selector', settings.selector, 'found', found);
            tries++; // important to eventually time out

            const stop = stopIf(found, loading, () => tries = 0);
            if (stop) stopTrying(true);
        }, settings.intervalDelayMs);
    }

    // exported API, register globally to enable calling it after
    const exports = window.FOMOcal = window.FOMOcal || {};

    exports.waitForSelector = {
        init: onFound => {
            console.info('waitForSelector.init');
            notifyFound = onFound;
        },

        onLoad: options => {
            console.info('waitForSelector.onLoad', options);
            withOptions(options);
            const startWaiting = () => { start(found => found > 0); };

            // start waiting for matches immediately or when DOM loads
            if (document.readyState === 'complete') startWaiting();
            else addEventListener('load', startWaiting);
        },

        afterScrollingDown: options => {
            console.info('waitForSelector.afterScrollingDown', options);
            withOptions(options);
            let lastFound = getMatchCount();
            if (settings.maxMatches <= lastFound) return notifyFound(true); // succeeded because we found maxMatches or more
            scrollDown(); // once initially

            start((found, loading, resetTimout) => {
                console.debug('afterScrollingDown found', found, 'loading', loading);
                // enough matches loaded
                if (settings.maxMatches <= found) return true; // succeeded because we found maxMatches or more

                if (lastFound !== found) { // more matches loaded
                    console.debug('loaded more. resetting tries.');
                    lastFound = found; // update found matches - important to time out
                    resetTimout(); // reset tries to achieve a sliding timeout if no new matches are loaded
                    scrollDown(); // to try trigger loading more
                    return; // to continue trying if loading isn't triggered immediately
                }

                // stop trying early if there are no more active loading requests
                if (loading <= 0) return true;
            });
        },

        afterClickingOn: (selector, options) => {
            console.info('waitForSelector.afterClickingOn', selector, options);
            withOptions(options);
            const element = document.querySelector(selector);
            console.info('trying to click', selector, 'found', element);

            if (element !== null) {
                element.scrollIntoView();
                element.click();
                const alreadyLoaded = getMatchCount();
                console.debug(settings.selector, 'already loaded', alreadyLoaded);
                // should only notifyFound(true) when loading more matches than alreadyLoaded were loaded
                start(found => alreadyLoaded < found);
            } else {
                notifyFound(false);
            }
        }
    };
})();

console.info('FOMOcal.waitForSelector attached.');
