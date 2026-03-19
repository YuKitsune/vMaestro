// @ts-check

/**
 * @type {import('@docusaurus/plugin-content-docs').SidebarsConfig}
 */
const sidebars = {
  userGuideSidebar: [
    {
      type: 'category',
      label: 'System Overview',
      items: [
        'user-guide/system-overview/concepts',
        'user-guide/system-overview/flight-processing',
        'user-guide/system-overview/online-mode',
      ],
    },
    {
      type: 'category',
      label: 'System Operation',
      items: [
        'user-guide/system-operation/interface',
        'user-guide/system-operation/tma-configuration',
        'user-guide/system-operation/flight-management',
        'user-guide/system-operation/slots',
        'user-guide/system-operation/coordination',
      ],
    },
  ],
  adminGuideSidebar: [
    'admin-guide/plugin-installation',
    'admin-guide/plugin-configuration',
    'admin-guide/server-deployment',
    'admin-guide/api-access',
  ],
};

export default sidebars;
