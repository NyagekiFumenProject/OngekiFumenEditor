if (!globalThis.LocalStorageInterop) {
    globalThis.LocalStorageInterop = (() => {
        function load(key) {
            return localStorage.getItem(key);
        }

        function save(key, value) {
            localStorage.setItem(key, value);
        }

        return {
            save, load
        };
    })
    ();

    console.log('localStorage.js initialized');
}