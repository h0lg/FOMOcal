(() => {
    const pickedClass = 'FOMOcal-picked',
        picked = '.' + pickedClass;

    let anchor = document.body,
        pickDescendant = true,
        notifyPicked;

    function intercept(event) {
        event.preventDefault();
        event.stopPropagation();
        pick(event.target);
    }

    function enable(enable) {
        const action = enable ? document.addEventListener : document.removeEventListener;

        /*  run in Capture phase to ensures that the handler intercepts the event
            before it reaches the target element's own event handlers, see
            https://developer.mozilla.org/en-US/docs/Web/API/EventTarget/addEventListener#usecapture */
        action.call(undefined, 'click', intercept, true);
    }

    function getClosestCommonAncestor(element, selector) {
        while (element) {
            if (element.querySelector(selector)) return element;
            element = element.parentElement;
        }

        return null;
    }

    function createCssSelector(element) {
            let selector = element.tagName.toLowerCase();
            if (element.id) selector += `#${element.id}`;

            if (element.className) {
                let classes = Array.from(element.classList);
                if (classes.length) selector += `.${classes.join('.')}`;
                let siblingIndex = Array.from(element.parentNode.children).indexOf(element) + 1;
                selector += `:nth-child(${siblingIndex})`;
            }

        return selector;
    }

    function getCssSelector(anchor, element) {
        let path = [];

        while (element !== anchor && element !== document.documentElement) {
            path.unshift(createCssSelector(element));
            element = element.parentNode;
        }

        return path.join(' > ');
    }

    function pick(target) {
        let css;

        if (pickDescendant) {
        const root = target.closest(anchor);
            console.info('picking descendant relative to', anchor);
            if (root === null) notifyPicked('');
            css = getCssSelector(root, target);
        } else {
            const root = getClosestCommonAncestor(target, anchor);
            if (root === null) notifyPicked('');
            css = createCssSelector(root);
            target = root;
        }

        console.info('picked', css);
        document.querySelectorAll(picked).forEach(el => el.classList.remove(pickedClass));
        target.classList.add(pickedClass);
        notifyPicked(css);
    }

    // exported API, register globally to enable calling it after
    const exports = window.FOMOcal = window.FOMOcal || {};

    exports.picking = { // exported API, register globally to enable calling it after
        init: onSelected => {
            notifyPicked = onSelected;
            const style = document.createElement('style');

            style.textContent = picked + `{
    filter: invert(100%);
    box-shadow: 0 0 10px 4px rgba(0, 128, 255, 0.8); /* Glow effect */
    outline: 2px solid rgba(0, 128, 255, 0.8); /* Optional outline */
}`;

            document.head.appendChild(style);
            enable(true);
        },

        enable,

        relativeTo: (anchorSelector, descendant) => {
            console.info('picking relative to', anchorSelector, 'descendant', descendant);
            anchor = anchorSelector;
            pickDescendant = descendant;
        },

        parent: () => { pick(document.querySelector(picked).parentNode); }
    };
})();

console.info('FOMOcal.picking attached.');
