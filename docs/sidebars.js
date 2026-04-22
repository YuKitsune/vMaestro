// @ts-check

/**
 * @type {import('@docusaurus/plugin-content-docs').SidebarsConfig}
 */
const sidebars = {
  userGuideSidebar: [
    'user-guide/system-overview',
    'user-guide/system-operation',
  ],
  adminGuideSidebar: [
    'admin-guide/plugin-installation',
    'admin-guide/plugin-configuration',
    'admin-guide/server-deployment',
    'admin-guide/api-access',
    'admin-guide/maestro-tools',
    {
      type: 'category',
      label: 'Migration Guides',
      link: null,
      items: [
        'admin-guide/migration-guides/v0.30-to-v0.31',
      ],
    },
  ],
};

export default sidebars;
