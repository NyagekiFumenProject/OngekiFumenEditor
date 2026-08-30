if (!globalThis.WindowInterop) {
    globalThis.WindowInterop = (() => {
        function isFullScreen() {
            return document.fullscreen;
        }

        function requestFullScreen() {
            document.documentElement.requestFullscreen();
        }

        function exitFullScreen() {
            if (document.fullscreenElement) {
                document.exitFullscreen();
            }
        }

        function openURL(url) {
            globalThis.open(url);
        }

        function setTitle(title) {
            document.title = title == null ? "" : String(title);
        }

        function setIcon(url) {
            const head = document.head;
            if (!head) {
                return;
            }

            let link = document.getElementById("favicon");
            if (!link) {
                link = head.querySelector('link[data-browser-icon="true"]');
            }
            if (!link) {
                link = document.createElement("link");
                link.id = "favicon";
                link.setAttribute("data-browser-icon", "true");
                head.appendChild(link);
            }

            link.rel = "icon";
            link.type = "image/x-icon";
            if (url) {
                link.href = String(url);
            } else {
                link.removeAttribute("href");
            }
        }

        function getDPI(url) {
            return window.devicePixelRatio || 1;
        }

        return {
            exitFullScreen,
            requestFullScreen,
            isFullScreen,
            openURL,
            setTitle,
            setIcon,
            getDPI
        };
    })();

    console.log("window.js initialized");
}
