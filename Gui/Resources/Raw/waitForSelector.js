function waitForSelector(selector, intervalDelayMs, callBack, maxTries) {
    let tries = 0;

    const startWaiting = () => {
        const intervalID = setInterval(() => {
            if (tries >= maxTries) {
                clearInterval(intervalID); /* stop trying */
                callBack(false);
            }

            tries++;
            if (document.querySelectorAll(selector).length < 1) return; /* not available, wait and try again */
            clearInterval(intervalID); /* no need to continue trying */
            callBack(true);
        }, intervalDelayMs);
    };

    if (document.readyState === 'complete') startWaiting();
    else addEventListener('load', startWaiting);
}
