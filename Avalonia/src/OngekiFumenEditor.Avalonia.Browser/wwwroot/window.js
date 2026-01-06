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

        function getDPI(url) {
            return window.devicePixelRatio || 1;
        }

        return {
            exitFullScreen, requestFullScreen, isFullScreen, openURL, getDPI
        };
    })();

    console.log("window.js initialized");
}