(function () {
    const STORAGE_KEY = "color-scheme";
    const darkMedia = window.matchMedia("(prefers-color-scheme: dark)");
    let memoryOverride = readStoredScheme();

    function getSystemScheme() {
        return darkMedia.matches ? "dark" : "light";
    }

    function readStoredScheme() {
        try {
            const value = localStorage.getItem(STORAGE_KEY);
            return value === "light" || value === "dark" ? value : null;
        } catch {
            return null;
        }
    }

    function getOverride() {
        try {
            const value = localStorage.getItem(STORAGE_KEY);
            memoryOverride = value === "light" || value === "dark" ? value : null;
        } catch {
            // Storage blocked; keep the in-memory override.
        }
        return memoryOverride;
    }

    function applyScheme(storedScheme) {
        const meta = document.querySelector('meta[name="color-scheme"]');
        if (meta) {
            meta.content = storedScheme ?? "light dark";
        }
        const effective = storedScheme ?? getSystemScheme();
        document.documentElement.classList.toggle("dark", effective === "dark");
    }

    function setOverride(scheme) {
        memoryOverride = scheme;
        try {
            if (scheme === null) {
                localStorage.removeItem(STORAGE_KEY);
            } else {
                localStorage.setItem(STORAGE_KEY, scheme);
            }
        } catch {
            // Storage blocked; still apply for the current session.
        }
        applyScheme(scheme);
    }

    function toggle() {
        const currentRendered = getOverride() ?? getSystemScheme();
        const target = currentRendered === "dark" ? "light" : "dark";
        const system = getSystemScheme();

        setOverride(target !== system ? target : null);
    }

    window.addEventListener("storage", function (event) {
        if (event.key !== STORAGE_KEY && event.key !== null) {
            return;
        }
        applyScheme(getOverride());
    });

    // Keep .dark class synced with system changes when following system default;
    // never deletes any stored override.
    darkMedia.addEventListener("change", function () {
        if (getOverride() === null) {
            applyScheme(null);
        }
    });

    window.bethuyaTheme = {
        toggle: toggle
    };

    applyScheme(getOverride());
})();
