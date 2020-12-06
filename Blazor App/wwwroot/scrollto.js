window.blazorHelpers = {
    scrollToFragment: (elementId) => {
        var element = document.getElementById(elementId);

        if (element) {
            var tree = document.getElementById("fileTreePanel");
            var pos = element.offsetTop - 15;
            tree.scroll({
                top: pos,
                behavior: 'smooth'
            });
        }
    },

    Split: (splitElem) => {
        instance = Split(splitElem);
    },

    Destroy: () => {
        instance.destroy();
    }
};