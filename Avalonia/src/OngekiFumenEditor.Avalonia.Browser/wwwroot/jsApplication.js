if (!globalThis.JsApplication) {
    globalThis.JsApplication = (() => {
        let isDirty = false;

        function beforeUnloadHandler(e) {
            // 浏览器只保证展示原生通用确认框，自定义文案会被忽略。
            e.preventDefault();
            e.returnValue = true;
        }

        function exit() {
            /*
            if (navigator.userAgent.indexOf("Firefox") != -1 || navigator.userAgent.indexOf("Chrome") != -1) {
                window.location.href = "about:blank";
                window.close();
            } else {
                window.opener = null;
                window.open("", "_self");
                window.close();
            }
            */
            window.location.href="about:blank";
            window.close();
        }

        function setDirtyState(dirty) {
            dirty = !!dirty;
            if (dirty === isDirty)
                return;
            isDirty = dirty;
            // 只在有未保存修改时拦截关闭/刷新，干净状态不打扰用户。
            if (isDirty)
                globalThis.addEventListener?.("beforeunload", beforeUnloadHandler);
            else
                globalThis.removeEventListener?.("beforeunload", beforeUnloadHandler);
        }

        return {
            exit,
            setDirtyState
        };
    })();

    console.log("jsApplication.js initialized");
}