testMethod
    "Test block temp indexing"
    | methodTemp |
    methodTemp := 1.
    [| blockTemp | blockTemp := 2. blockTemp] value
