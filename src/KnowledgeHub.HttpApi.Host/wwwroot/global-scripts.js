/* Global login helpers */
(() => {
  const LOGIN_PATH_REGEX = /\/Account\/Login\/?$/i;

  const fetchTenants = async () => {
    try {
      const response = await fetch('/api/public/tenants', { credentials: 'same-origin' });
      if (response.ok) {
        return await response.json();
      }
    } catch (error) {
      console.warn('Failed to fetch tenants:', error);
    }

    return [{ id: null, name: '全局' }];
  };

  const getTenantRow = () => {
    const tenantSwitchLink = document.getElementById('AbpTenantSwitchLink');
    return tenantSwitchLink?.closest('.row') ?? null;
  };

  const setSelectedTenant = (tenant) => {
    const tenantId = tenant?.id || '';
    const tenantName = tenant?.name || '全局';

    localStorage.setItem('selectedTenantName', tenantName);
    localStorage.setItem('selectedTenantId', tenantId);
    document.cookie = `__tenant=${tenantId};path=/`;
    window.location.reload();
  };

  const renderTenantList = async () => {
    const tenantRow = getTenantRow();
    if (!tenantRow || document.querySelector('.kh-tenant-list')) {
      return;
    }

    const tenantSwitchLink = document.getElementById('AbpTenantSwitchLink');
    if (tenantSwitchLink) {
      tenantSwitchLink.style.display = 'none';
    }

    const tenants = await fetchTenants();
    const selectedTenantId = localStorage.getItem('selectedTenantId') || '';
    const selectedTenantName = localStorage.getItem('selectedTenantName');

    const nameSpan = tenantRow.querySelector('.col span');
    if (nameSpan && selectedTenantName) {
      nameSpan.textContent = selectedTenantName;
    }

    const container = document.createElement('div');
    container.className = 'kh-tenant-list';

    const title = document.createElement('div');
    title.className = 'kh-tenant-list-title';
    title.textContent = '选择租户';
    container.appendChild(title);

    const list = document.createElement('div');
    list.className = 'kh-tenant-list-options';

    tenants.forEach(tenant => {
      const tenantId = tenant.id || '';
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'kh-tenant-option';
      if (tenantId === selectedTenantId) {
        button.classList.add('active');
      }
      button.textContent = tenant.name || '全局';
      button.addEventListener('click', () => setSelectedTenant(tenant));
      list.appendChild(button);
    });

    container.appendChild(list);
    tenantRow.insertAdjacentElement('afterend', container);
  };

  const run = () => {
    if (!LOGIN_PATH_REGEX.test(window.location.pathname)) {
      return;
    }

    renderTenantList();
  };

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', run);
  } else {
    run();
  }
})();
