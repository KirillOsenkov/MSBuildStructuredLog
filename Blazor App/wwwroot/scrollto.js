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
    },

    DarkMode: () => {
        var app = document.getElementById("app");
        if (app)
        {
            app.style.backgroundColor = "#202020";
            app.style.color = "#E0E0E0"
        }
        
        var main = document.getElementById("mainLayout");
        if (main)
        {
            main.style.backgroundColor = "#202020";
            main.style.color = "#E0E0E0"
        }

        var tab = document.getElementById("ui-tabpanel-0");
        if (!tab)
        {
            tab = document.getElementById("ui-tabpanel-1");
        }
        if (!tab)
        {
            tab = document.getElementById("ui-tabpanel-2");
        }
        tab.style.backgroundColor = "#202020";
    },

    LightMode: () => {
        var app = document.getElementById("app");
        if (app)
        {
            app.style.backgroundColor = "white";
            app.style.color = "black"
        }

        var main = document.getElementById("mainLayout");
        if (main)
        {
            main.style.backgroundColor = "white";
            main.style.color = "black"
        }

        var tab = document.getElementById("ui-tabpanel-0");
        if (!tab) {
            tab = document.getElementById("ui-tabpanel-1");
        }
        if (!tab) {
            tab = document.getElementById("ui-tabpanel-2");
        }
        tab.style.backgroundColor = "white";
    }
};