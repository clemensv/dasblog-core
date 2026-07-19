(() => {
    const tabRoot = document.querySelector("[data-settings-tabs]");
    if (!tabRoot) {
        return;
    }

    const tabs = Array.from(tabRoot.querySelectorAll('[role="tab"]'));
    const panels = tabs.map((tab) => document.getElementById(tab.getAttribute("aria-controls")));

    const reveal = (tab) => {
        const tabStart = tab.offsetLeft;
        const tabEnd = tabStart + tab.offsetWidth;
        if (tabStart < tabRoot.scrollLeft) {
            tabRoot.scrollTo({ left: tabStart });
        } else if (tabEnd > tabRoot.scrollLeft + tabRoot.clientWidth) {
            tabRoot.scrollTo({ left: tabEnd - tabRoot.clientWidth });
        }
    };

    const activate = (tab, focus = false, updateHash = true) => {
        tabs.forEach((candidate, index) => {
            const selected = candidate === tab;
            candidate.setAttribute("aria-selected", selected.toString());
            candidate.tabIndex = selected ? 0 : -1;
            panels[index].hidden = !selected;
        });

        reveal(tab);

        if (focus) {
            tab.focus();
        }

        if (updateHash) {
            history.replaceState(null, "", `#${tab.getAttribute("aria-controls")}`);
        }
    };

    tabs.forEach((tab, index) => {
        tab.addEventListener("click", () => activate(tab));
        tab.addEventListener("keydown", (event) => {
            let nextIndex;
            if (event.key === "ArrowRight") {
                nextIndex = (index + 1) % tabs.length;
            } else if (event.key === "ArrowLeft") {
                nextIndex = (index - 1 + tabs.length) % tabs.length;
            } else if (event.key === "Home") {
                nextIndex = 0;
            } else if (event.key === "End") {
                nextIndex = tabs.length - 1;
            } else {
                return;
            }

            event.preventDefault();
            activate(tabs[nextIndex], true);
        });
    });

    document.getElementById("UpdateSettingsForm")?.addEventListener("invalid", (event) => {
        const panel = event.target.closest('[role="tabpanel"]');
        const tab = tabs.find((candidate) => candidate.getAttribute("aria-controls") === panel?.id);
        if (tab) {
            activate(tab);
        }
    }, true);

    const invalidControl = document.querySelector(".input-validation-error, [aria-invalid='true']");
    const invalidPanel = invalidControl?.closest('[role="tabpanel"]');
    const requestedPanel = invalidPanel?.id || window.location.hash.substring(1);
    const requestedTab = tabs.find((tab) => tab.getAttribute("aria-controls") === requestedPanel);
    activate(requestedTab || tabs[0], false, false);
})();