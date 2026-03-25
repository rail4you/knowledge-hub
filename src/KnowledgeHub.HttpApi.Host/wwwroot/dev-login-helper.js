/* Dev Login Helper */
(() => {
  const defaultAdminUsername = "admin";
  const defaultAdminPassword = "1q2w3E*";

  const style = document.createElement('style');
  style.textContent = `
    .tenant-selector-popup { font-size: 14px !important; }
    .tenant-selector-title { font-size: 16px !important; font-weight: 600 !important; }
    .tenant-selector-input { font-size: 14px !important; padding: 8px !important; }
    .tenant-selector-btn { font-size: 14px !important; padding: 8px 16px !important; }
  `;
  document.head.appendChild(style);

  const fetchTenants = async () => {
    try {
      const response = await fetch("/api/public/tenants");
      if (response.ok) {
        return await response.json();
      }
    } catch (e) {
      console.warn("Failed to fetch tenants:", e);
    }
    return [{ id: null, name: "全局" }];
  };

  const showTenantSelector = async () => {
    const tenants = await fetchTenants();
    const savedTenantName = localStorage.getItem("selectedTenantName") || "";
    
    const tenant = tenants.find(t => t.name === savedTenantName);
    const selectedId = tenant ? tenant.id : null;

    const { value: selectedValue } = await Swal.fire({
      title: '选择租户',
      input: 'select',
      inputOptions: tenants.reduce((acc, t) => {
        acc[t.id || ''] = t.name;
        return acc;
      }, {}),
      inputValue: selectedId || '',
      showCancelButton: true,
      confirmButtonText: '确定',
      cancelButtonText: '取消',
      customClass: {
        popup: 'tenant-selector-popup',
        title: 'tenant-selector-title',
        input: 'tenant-selector-input',
        confirmButton: 'tenant-selector-btn',
        cancelButton: 'tenant-selector-btn'
      },
      inputValidator: (value) => {
        return new Promise((resolve) => {
          resolve();
        });
      }
    });

    if (selectedValue !== undefined) {
      const selectedTenant = tenants.find(t => (t.id || '') === selectedValue);
      const tenantName = selectedTenant ? selectedTenant.name : '全局';
      localStorage.setItem("selectedTenantName", tenantName);
      localStorage.setItem("selectedTenantId", selectedValue || "");
      
      document.cookie = `__tenant=${selectedValue || ''};path=/`;
      
      const nameSpan = document.querySelector('.tenant-info .tenant-name, .card .row .col span, #AbpTenantSwitchLink').closest('.row').querySelector('span');
      if (nameSpan) {
        nameSpan.textContent = tenantName;
      }
      
      location.reload();
    }
  };

  const run = () => {
    if (!/\/Account\/Login\/?$/i.test(window.location.pathname)) {
      return;
    }

    const userInput = document.querySelector(
      'input[name$="UserNameOrEmailAddress"], input[id$="UserNameOrEmailAddress"], input[name$="UserName"], input[id$="UserName"]'
    );
    const passwordInput = document.querySelector(
      'input[type="password"][name$="Password"], input[type="password"][id$="Password"]'
    );

    const addHint = (input, text) => {
      if (!input || input.dataset.defaultHintAdded === "true") {
        return;
      }

      const hint = document.createElement("span");
      hint.className = "text-muted small";
      hint.textContent = text;

      const floatingContainer = input.closest(".form-floating.mb-2");
      if (floatingContainer) {
        floatingContainer.appendChild(hint);
        input.dataset.defaultHintAdded = "true";
        return;
      }

      const container =
        input.closest(".input-group") ||
        input.closest(".form-group") ||
        input.closest(".mb-3") ||
        input.closest(".form-floating") ||
        input;

      container.insertAdjacentElement("afterend", hint);
      input.dataset.defaultHintAdded = "true";
    };

    const autoFillDefaults = () => {
      if (userInput && !userInput.value) {
        userInput.value = defaultAdminUsername;
      }
      if (passwordInput && !passwordInput.value) {
        passwordInput.value = defaultAdminPassword;
      }
    };

    addHint(userInput, `Default username: ${defaultAdminUsername}`);
    addHint(passwordInput, `Default password: ${defaultAdminPassword}`);

    setTimeout(autoFillDefaults, 150);
    if (userInput) {
      userInput.addEventListener("focus", autoFillDefaults, { once: true });
    }
    if (passwordInput) {
      passwordInput.addEventListener("focus", autoFillDefaults, { once: true });
    }

    const tenantSwitchLink = document.getElementById('AbpTenantSwitchLink');
    if (tenantSwitchLink) {
      const savedTenantName = localStorage.getItem("selectedTenantName");
      if (savedTenantName) {
        const tenantNameSpan = tenantSwitchLink.closest('.row')?.querySelector('.col span');
        if (tenantNameSpan) {
          tenantNameSpan.textContent = savedTenantName;
        }
      }
      
      tenantSwitchLink.addEventListener('click', (e) => {
        e.preventDefault();
        e.stopImmediatePropagation();
        showTenantSelector();
      }, true);
    }
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", run);
  } else {
    run();
  }
})();
