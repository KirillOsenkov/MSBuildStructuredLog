window.blazorHelpers = {
    scrollToFragment: (elementId) => {
        var element = document.getElementById(elementId);

        if (element) {
            var tree = document.getElementById("five");
            if (!tree) {
                tree = document.getElementById("two");
            }
            var pos = element.offsetTop - 15;
            tree.scroll({
                top: pos,
                behavior: 'smooth'
            });
        }
    }
};