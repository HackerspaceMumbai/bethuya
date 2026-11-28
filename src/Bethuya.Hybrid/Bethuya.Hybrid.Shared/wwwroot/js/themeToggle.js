(function () {
    const STORAGE_KEY = "color-scheme";
    const darkMedia = window.matchMedia("(prefers-color-scheme: dark)");

    function getSystemScheme() {
        return darkMedia.matches ? "dark" : "light";
    }

    function getStoredScheme() {
        try {
            const value = localStorage.getItem(STORAGE_KEY);
            return value === "light" || value === "dark" ? value : null;
        } catch {
            return null;
        }
    }

    function applyScheme(storedScheme) {
        const meta = document.querySelector('meta[name="color-scheme"]');
        if (meta) {
            meta.content = storedScheme ?? "light dark";
        }
        const effective = storedScheme ?? getSystemScheme();
        document.documentElement.classList.toggle("dark", effective === "dark");
    }

    function toggle() {
        const currentRendered = getStoredScheme() ?? getSystemScheme();
        const target = currentRendered === "dark" ? "light" : "dark";
        const system = getSystemScheme();

        if (target !== system) {
            try {
                localStorage.setItem(STORAGE_KEY, target);
            } catch {
                // Storage blocked; still apply for the current session.
            }
            applyScheme(target);
        } else {
            try {
                localStorage.removeItem(STORAGE_KEY);
            } catch {
                // Storage blocked; still apply for the current session.
            }
            applyScheme(null);
        }
    }

    window.addEventListener("storage", function (event) {
        if (event.key === STORAGE_KEY) {
            applyScheme(getStoredScheme());
        }
    });

    // Keep .dark class synced with system changes when following system default;
    // never deletes any stored override.
    darkMedia.addEventListener("change", function () {
        if (getStoredScheme() === null) {
            applyScheme(null);
        }
    });

    window.bethuyaTheme = {
        toggle: toggle
    };

    applyScheme(getStoredScheme());
})();
