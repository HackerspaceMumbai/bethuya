window.bethuyaOpportunityEngine = window.bethuyaOpportunityEngine || {};

window.bethuyaOpportunityEngine.scrollToSection = function (sectionId) {
    const section = document.getElementById(sectionId);
    if (!section) {
        return;
    }

    const reduceMotion = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    section.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
    if (!section.hasAttribute("tabindex")) {
        section.setAttribute("tabindex", "-1");
    }

    section.focus({ preventScroll: true });
};
