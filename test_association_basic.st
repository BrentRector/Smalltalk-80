"Basic test of Association creation via message send"
| result |

"Test creating an Association instance using key:value: class method"
result := Association key: #TestKey value: 123.

"If we get here without errors, Association creation worked"
^result