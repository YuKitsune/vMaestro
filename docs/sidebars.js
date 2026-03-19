// @ts-check

// This runs in Node.js - Don't use client-side code here (browser APIs, JSX...)

/**
 * Creating a sidebar enables you to:
 - create an ordered group of docs
 - render a sidebar for each doc of that group
 - provide next/previous navigation

 The sidebars can be generated from the filesystem, or explicitly defined here.

 Create as many sidebars as you want.

 @type {import('@docusaurus/plugin-content-docs').SidebarsConfig}
 */
const sidebars = {
  docsSidebar: [
    'index',
    {
      type: 'category',
      label: 'User Guide',
      link: {
        type: 'doc',
        id: 'user-guide/index',
      },
      items: [
        {
          type: 'category',
          label: 'System Overview',
          link: {
            type: 'doc',
            id: 'user-guide/system-overview/index',
          },
          items: [
            'user-guide/system-overview/01-concepts',
            'user-guide/system-overview/02-flight-processing',
            'user-guide/system-overview/03-online-mode',
          ],
        },
        {
          type: 'category',
          label: 'System Operation',
          link: {
            type: 'doc',
            id: 'user-guide/system-operation/index',
          },
          items: [
            'user-guide/system-operation/01-interface',
            'user-guide/system-operation/02-tma-configuration',
            'user-guide/system-operation/03-flight-management',
            'user-guide/system-operation/04-slots',
            'user-guide/system-operation/05-coordination',
          ],
        },
      ],
    },
    {
      type: 'category',
      label: 'Admin Guide',
      link: {
        type: 'doc',
        id: 'admin-guide/index',
      },
      items: [
        'admin-guide/01-plugin-installation',
        'admin-guide/02-plugin-configuration',
        'admin-guide/03-server-deployment',
        'admin-guide/04-api-access',
      ],
    },
  ],
};

export default sidebars;
