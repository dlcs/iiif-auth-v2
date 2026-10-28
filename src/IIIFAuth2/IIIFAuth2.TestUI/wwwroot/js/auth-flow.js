// Minimal IIIF Authorization Flow 2.0 client, see https://iiif.io/api/auth/2.0/
// Logs each step along with any downstream calls the fake Orchestrator made to iiif-auth-v2.
(() => {
    const config = window.authFlowConfig;
    const origin = window.location.origin;
    const logEl = document.getElementById("log");
    const tokenInput = document.getElementById("token");
    const rewriteProbe = document.getElementById("rewriteProbe");
    const accessServicesEl = document.getElementById("accessServices");
    const contentEl = document.getElementById("content");

    let lastCallId = config.lastCallId;
    let contentResource = null;
    let probeService = null;

    // --- logging ---
    function log(message, data, cssClass) {
        const entry = document.createElement("div");
        entry.className = "entry" + (cssClass ? " " + cssClass : "");
        const time = document.createElement("span");
        time.className = "time";
        time.textContent = new Date().toISOString().substring(11, 23);
        entry.append(time, message);
        if (data !== undefined) {
            const details = document.createElement("details");
            const summary = document.createElement("summary");
            summary.textContent = "details";
            const pre = document.createElement("pre");
            pre.textContent = typeof data === "string" ? data : JSON.stringify(data, null, 2);
            details.append(summary, pre);
            entry.append(details);
        }
        logEl.append(entry);
        logEl.scrollTop = logEl.scrollHeight;
    }

    async function logDownstreamCalls() {
        try {
            const response = await fetch(`/calls/json?after=${lastCallId}`);
            const calls = await response.json();
            for (const call of calls) {
                lastCallId = Math.max(lastCallId, call.id);
                const status = call.statusCode ?? `failed: ${call.error}`;
                log(`↳ downstream ${call.method} ${call.url} → ${status} (${call.durationMs}ms)`, {
                    requestHeaders: call.requestHeaders,
                    responseHeaders: call.responseHeaders,
                    body: tryParseJson(call.responseBody) ?? call.error
                }, "downstream");
            }
        } catch (e) {
            log(`Unable to load downstream calls: ${e}`, undefined, "error");
        }
    }

    function tryParseJson(value) {
        if (!value) return value;
        try { return JSON.parse(value); } catch { return value; }
    }

    const firstOfType = (services, type) => (services ?? []).find(s => s.type === type || s["@type"] === type);
    const allOfType = (services, type) => (services ?? []).filter(s => s.type === type || s["@type"] === type);
    const languageValue = (map) => map ? Object.values(map).flat().join(" ") : "";

    // --- flow ---
    // Run every step, only requiring user interaction if access service needs it. Returns true if content accessible
    async function runAll() {
        // Open window while still handling click, popup blockers would prevent this after awaiting other requests
        const accessWindow = window.open("about:blank");
        const closeWindow = () => accessWindow?.close();
        log("Running full flow (blank window opened now to avoid popup blocker)");

        if (await start()) {
            closeWindow();
            log("Flow complete: content accessible", undefined, "success");
            return true;
        }

        const accessServices = allOfType(probeService?.service, "AuthAccessService2");
        const accessService = accessServices.find(s => s.profile !== "external") ?? accessServices[0];
        const tokenService = firstOfType(accessService?.service, "AuthAccessTokenService2");
        if (!accessService || !tokenService) {
            closeWindow();
            log("Flow complete: not authorised, no usable access service", undefined, "error");
            return false;
        }

        log("Trying token service before access service, succeeds if there is an existing session");
        let authorised = await requestTokenAndProbe(tokenService);

        if (!authorised && accessService.profile !== "external") {
            if (accessServices.length > 1) log(`${accessServices.length} access services, using first: ${accessService.id}`);
            authorised = await openAccessService(accessService, tokenService, accessWindow);
        } else {
            closeWindow();
        }

        log(`Flow complete: ${authorised ? "content accessible" : "not authorised"}`, undefined,
            authorised ? "success" : "error");
        return authorised;
    }

    // Load manifest and probe. Returns true if content accessible
    async function start() {
        if (!(await loadManifest())) return false;
        if (!probeService) {
            log("No AuthProbeService2 on content resource - treating as open");
            await loadContent();
            return true;
        }
        return await probeAndLoad();
    }

    // Load manifest if not already loaded, so individual steps can be run without starting flow
    async function ensureManifest() {
        if (contentResource) return true;
        log("Manifest not loaded yet, loading");
        return await loadManifest();
    }

    // Load manifest and find content resource + probe service. Returns false if manifest unusable
    async function loadManifest() {
        contentResource = null;
        probeService = null;
        accessServicesEl.textContent = "";
        contentEl.textContent = "Not loaded.";

        log(`Fetching manifest ${config.manifestUrl}`);
        const response = await fetch(config.manifestUrl);
        await logDownstreamCalls();
        if (!response.ok) {
            log(`Manifest request failed: ${response.status}`, undefined, "error");
            return false;
        }
        const manifest = await response.json();
        log("Manifest loaded", manifest);

        contentResource = manifest.items?.[0]?.items?.[0]?.items?.[0]?.body;
        if (!contentResource) {
            log("No content resource found on canvas", undefined, "error");
            return false;
        }
        log(`Content resource ${contentResource.id}`);

        probeService = firstOfType(contentResource.service, "AuthProbeService2");
        if (probeService) {
            log(`Found probe service ${probeService.id}`, probeService);
            renderAccessServices(allOfType(probeService.service, "AuthAccessService2"));
            restoreToken();
        }
        return true;
    }

    // --- token storage ---
    // Tokens are kept per token service for this browser tab, as a client would reuse them across resources
    const tokenKey = (tokenService) => `iiif-auth-token:${tokenService.id}`;

    function storeToken(tokenService, token) {
        try {
            if (token) sessionStorage.setItem(tokenKey(tokenService), token);
            else sessionStorage.removeItem(tokenKey(tokenService));
        } catch { /* storage unavailable, token only held in page */ }
    }

    function restoreToken() {
        if (tokenInput.value.trim()) return;
        for (const accessService of allOfType(probeService?.service, "AuthAccessService2")) {
            const tokenService = firstOfType(accessService.service, "AuthAccessTokenService2");
            let token = null;
            try { token = tokenService && sessionStorage.getItem(tokenKey(tokenService)); } catch { }
            if (token) {
                tokenInput.value = token;
                log(`Reusing access token issued earlier in this tab by ${tokenService.id}`);
                return;
            }
        }
    }

    function getProbeUrl() {
        if (!rewriteProbe.checked) return probeService.id;
        const url = new URL(probeService.id);
        return origin + url.pathname + url.search;
    }

    async function probe() {
        if (!(await ensureManifest())) return null;
        if (!probeService) {
            log("No probe service on content resource - nothing to probe");
            return null;
        }
        const url = getProbeUrl();
        const token = tokenInput.value.trim();
        const headers = token ? { Authorization: `Bearer ${token}` } : {};
        log(`Probing ${url} ${token ? "with" : "without"} bearer token`);

        try {
            const response = await fetch(url, { headers });
            await logDownstreamCalls();
            const body = await response.text();
            const probeResult = tryParseJson(body);
            if (!response.ok || typeof probeResult !== "object") {
                log(`Probe request failed: HTTP ${response.status}`, body, "error");
                return null;
            }
            const message = `Probe result status ${probeResult.status} ${languageValue(probeResult.heading)}`;
            log(message, probeResult, probeResult.status === 200 ? "success" : "error");
            return probeResult;
        } catch (e) {
            log(`Probe request error: ${e}`, undefined, "error");
            return null;
        }
    }

    // Returns true if probe status 200 and content loaded
    async function probeAndLoad() {
        const probeResult = await probe();
        if (probeResult?.status === 200) {
            await loadContent();
            return true;
        }
        if (probeResult) log("Not authorised - use an access service below to authenticate");
        return false;
    }

    async function loadContent() {
        if (!(await ensureManifest())) return;
        log(`Requesting content ${contentResource.id} (browser sends cookies)`);
        try {
            const response = await fetch(contentResource.id, { credentials: "include" });
            await logDownstreamCalls();
            log(`Content response HTTP ${response.status}`, undefined, response.ok ? "success" : "error");
            const blob = await response.blob();
            const img = document.createElement("img");
            img.src = URL.createObjectURL(blob);
            img.alt = `Content resource, HTTP ${response.status}`;
            contentEl.replaceChildren(img);
        } catch (e) {
            log(`Content request error: ${e}`, undefined, "error");
        }
    }

    // --- access services ---
    function renderAccessServices(accessServices) {
        accessServicesEl.replaceChildren();
        if (accessServices.length === 0) {
            accessServicesEl.textContent = "Probe service has no access services.";
            return;
        }

        for (const accessService of accessServices) {
            const card = document.createElement("div");
            card.className = "card";

            const title = document.createElement("div");
            title.innerHTML = "<strong></strong> (profile <code></code>)";
            title.querySelector("strong").textContent = languageValue(accessService.label) || accessService.id;
            title.querySelector("code").textContent = accessService.profile;

            const detail = document.createElement("div");
            detail.className = "hint";
            detail.textContent = [languageValue(accessService.heading), languageValue(accessService.note)]
                .filter(Boolean).join(" - ");

            const buttons = document.createElement("div");
            const addButton = (text, handler) => {
                const button = document.createElement("button");
                button.type = "button";
                button.textContent = text;
                button.addEventListener("click", handler);
                buttons.append(button);
            };

            const tokenService = firstOfType(accessService.service, "AuthAccessTokenService2");
            const logoutService = firstOfType(accessService.service, "AuthLogoutService2");

            if (accessService.profile !== "external") {
                const confirmLabel = languageValue(accessService.confirmLabel) || "Open access service";
                addButton(confirmLabel, () => openAccessService(accessService, tokenService));
            }
            if (tokenService) addButton("Request token only", () => requestTokenAndProbe(tokenService));
            if (logoutService) {
                addButton(languageValue(logoutService.label) || "Logout", () => logout(logoutService, tokenService));
            }

            card.append(title, detail, buttons);
            accessServicesEl.append(card);
        }
    }

    // Open access service, optionally in an already open window. Once closed, request token and probe.
    // Returns promise resolving to true if content accessible
    function openAccessService(accessService, tokenService, existingWindow) {
        const url = `${accessService.id}?origin=${encodeURIComponent(origin)}`;
        log(`Opening access service (${accessService.profile}) in new window ${url}`, accessService);
        let accessWindow = existingWindow;
        if (accessWindow && !accessWindow.closed) {
            accessWindow.location.href = url;
        } else {
            accessWindow = window.open(url);
        }
        if (!accessWindow) {
            log("Unable to open window - is a popup blocker active?", undefined, "error");
            return Promise.resolve(false);
        }

        return new Promise(resolve => {
            const timer = setInterval(async () => {
                if (!accessWindow.closed) return;
                clearInterval(timer);
                log("Access service window closed");
                if (tokenService) {
                    resolve(await requestTokenAndProbe(tokenService));
                } else {
                    log("Access service has no token service", undefined, "error");
                    resolve(false);
                }
            }, 500);
        });
    }

    function requestToken(tokenService) {
        const messageId = crypto.randomUUID();
        const url = `${tokenService.id}?messageId=${messageId}&origin=${encodeURIComponent(origin)}`;
        log(`Requesting access token via iframe ${url}`);

        return new Promise(resolve => {
            const iframe = document.createElement("iframe");
            iframe.style.display = "none";

            const finish = (result) => {
                clearTimeout(timeout);
                window.removeEventListener("message", onMessage);
                iframe.remove();
                resolve(result);
            };
            const onMessage = (event) => {
                if (event.data?.messageId !== messageId) return;
                log(`Received postMessage from ${event.origin}`, event.data);
                finish(event.data);
            };
            const timeout = setTimeout(() => {
                log("Timed out waiting for token service postMessage", undefined, "error");
                finish(null);
            }, 10000);

            window.addEventListener("message", onMessage);
            iframe.src = url;
            document.body.append(iframe);
        });
    }

    // Returns true if token received and content accessible
    async function requestTokenAndProbe(tokenService) {
        const tokenResponse = await requestToken(tokenService);
        if (tokenResponse?.type === "AuthAccessToken2") {
            tokenInput.value = tokenResponse.accessToken;
            storeToken(tokenService, tokenResponse.accessToken);
            log(`Received access token, expires in ${tokenResponse.expiresIn}s`, undefined, "success");
            return await probeAndLoad();
        }
        if (tokenResponse?.type === "AuthAccessTokenError2") {
            storeToken(tokenService, null);
            const message = `Token error '${tokenResponse.profile}': ${languageValue(tokenResponse.heading)} ${languageValue(tokenResponse.note)}`;
            log(message, undefined, "error");
        }
        return false;
    }

    function logout(logoutService, tokenService) {
        log(`Opening logout service ${logoutService.id} and discarding access token`);
        tokenInput.value = "";
        if (tokenService) storeToken(tokenService, null);
        window.open(logoutService.id);
    }

    // --- wire up ---
    const safe = (fn) => async () => {
        try { await fn(); } catch (e) { log(`Unexpected error: ${e}`, e?.stack, "error"); }
    };
    document.getElementById("runAll").addEventListener("click", safe(runAll));
    document.getElementById("start").addEventListener("click", safe(start));
    document.getElementById("probe").addEventListener("click", safe(probeAndLoad));
    document.getElementById("loadContent").addEventListener("click", safe(loadContent));
    document.getElementById("clearLog").addEventListener("click", () => logEl.replaceChildren());
})();
