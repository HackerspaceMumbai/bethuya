window.bethuyaOpportunityEngine = window.bethuyaOpportunityEngine || {};

window.bethuyaOpportunityEngine.scrollToSection = function (sectionId) {
    const section = document.getElementById(sectionId);
    if (!section) {
        return;
    }

    section.scrollIntoView({ behavior: "smooth", block: "start" });
    if (!section.hasAttribute("tabindex")) {
        section.setAttribute("tabindex", "-1");
    }

    section.focus({ preventScroll: true });
};
