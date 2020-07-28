window.blazorHelpers = {
    scrollToFragment: (elementId) => {
        var element = document.getElementById(elementId);

        if (element) {
            var tree = document.getElementById("two");
            var pos = element.offsetTop - 15;
            tree.scroll({
                top: pos,
                behavior: 'smooth'
            });
        }
    },

    HideSource: () => {
        var element = document.getElementById("three").style.visibility = "hidden";
    },


    ShowSource: () => {
        var element = document.getElementById("three").style.visibility = "visible";

    },

    Split: (splitElem) => {
        instance = Split(splitElem);
    },

    Destroy: () => {
        instance.destroy();
    }
};