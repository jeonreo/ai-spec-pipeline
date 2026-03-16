{
  "version": "1.0",
  "meta": {
    "screenId": "[screen-id]",
    "screenName": "[screen name]",
    "screenType": "detail",
    "sourceSpecVersion": "spec-v1",
    "designSystemTarget": "clover-admin",
    "locale": "ko-KR"
  },
  "purpose": {
    "summary": "[screen purpose summary]",
    "primaryUser": "[primary user]",
    "businessGoal": "[business goal]",
    "successCriteria": [
      "[success criterion 1]",
      "[success criterion 2]"
    ]
  },
  "layout": {
    "pattern": "detail-with-sections",
    "structure": [
      "[block 1]",
      "[block 2]",
      "[block 3]"
    ],
    "density": "comfortable",
    "priority": [
      "[priority 1]",
      "[priority 2]",
      "[priority 3]"
    ]
  },
  "sections": [
    {
      "id": "[section-id]",
      "title": "[section title]",
      "role": "summary",
      "description": "[section purpose]",
      "contents": [
        "[content 1]",
        "[content 2]"
      ]
    }
  ],
  "dataModel": {
    "entities": [
      "[entity 1]"
    ],
    "keyFields": [
      "[field 1]",
      "[field 2]"
    ],
    "tableColumns": [
      "[column 1]",
      "[column 2]"
    ]
  },
  "components": {
    "recommended": [
      "PageHeader",
      "Card",
      "Badge",
      "Button",
      "Table",
      "Modal"
    ],
    "requiredBehaviors": [
      "[required behavior 1]"
    ],
    "forbiddenPatterns": [
      "[forbidden pattern 1]"
    ]
  },
  "states": {
    "screenStates": [
      "loading",
      "empty",
      "error",
      "ready"
    ],
    "componentStates": [
      "[component state 1]"
    ],
    "statusLabels": [
      "[status 1]",
      "[status 2]"
    ]
  },
  "interactions": {
    "rules": [
      "[interaction rule 1]",
      "[interaction rule 2]"
    ],
    "accessibility": [
      "keyboard navigable",
      "visible focus",
      "semantic labels"
    ]
  },
  "styleNotes": {
    "tone": "[visual tone]",
    "visualPriority": [
      "[priority 1]",
      "[priority 2]"
    ],
    "notes": [
      "[style note 1]",
      "[style note 2]"
    ]
  },
  "handoff": {
    "figmaMakePrompt": "[prompt for Figma Make]",
    "implementationNotes": [
      "[implementation note 1]",
      "[implementation note 2]"
    ]
  },
  "adapterHints": {
    "clover": {
      "layoutPattern": "list-layout",
      "menuGroup": "[menu group]",
      "menuItems": [
        "[menu item 1]",
        "[menu item 2]"
      ],
      "breadcrumbs": [
        "[breadcrumb 1]",
        "[breadcrumb 2]"
      ],
      "toolbar": {
        "primarySearchPlaceholder": "[search placeholder 1]",
        "secondarySearchPlaceholder": "[search placeholder 2 — optional, omit if single search]",
        "filterChips": [
          { "label": "[filter label]", "icon": "[optional icon char]", "active": false }
        ],
        "filterResetLabel": "RESET",
        "resultLabel": "[e.g. Results 5]",
        "rowsPerPage": 20
      },
      "extInfo": [
        { "label": "[meta label]", "value": "[meta value]", "badge": "[optional badge style]" }
      ],
      "leftPanel": {
        "title": "[left panel title]",
        "subtitle": "[left panel subtitle]",
        "badges": [
          "[badge 1]"
        ],
        "fields": [
          {
            "label": "[field label]",
            "value": "[field value]",
            "badge": "[optional badge]"
          }
        ],
        "actions": [
          "[action 1]",
          "[action 2]"
        ]
      },
      "summaryGrid": [
        {
          "label": "[summary label]",
          "value": "[summary value]",
          "subValue": "[optional sub value]"
        }
      ],
      "cards": [
        {
          "title": "[card title]",
          "subtitle": "[card subtitle]",
          "status": "[card status]",
          "meta": [
            {
              "label": "[meta label]",
              "value": "[meta value]"
            }
          ],
          "bullets": [
            "[bullet 1]"
          ],
          "actions": [
            "[card action 1]"
          ],
          "options": [
            { "label": "[action label]", "variant": "default" },
            { "label": "[danger action]", "variant": "danger" }
          ]
        }
      ],
      "table": {
        "columns": [
          { "key": "[col-key]", "label": "[Column Label]", "sortable": true, "infoTooltip": "[optional tooltip text]" }
        ],
        "rows": [
          {
            "[col-key]": "[plain string value]",
            "[badge-col-key]": { "type": "badge-stack", "items": [{ "label": "[badge label]", "style": "badge-green" }] }
          }
        ],
        "extInfo": [
          { "label": "[meta label]", "value": "[meta value]", "badge": "[optional badge style]" }
        ]
      },
      "badgeMapping": {
        "[status label]": "[badge style]"
      },
      "preferredBlocks": [
        "[block hint 1]"
      ]
    }
  }
}
