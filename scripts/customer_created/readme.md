# Customer Created

Python script to help in creating the default clickthrough access-service, role and role-provider for existing IIIF-CS customers. This is helpful when deploying IIIF-Auth-v2 Management to an existing IIIF-CS instance that already has customers.

To run create a file `sample.json` at the same level as this readme that has format:

```json
[
    { "id": -1, "name": "first-customer" },
    { "id": -2, "name": "second-customer" }
]
```

This can be generated via `SELECT "Id" AS id, "Name" AS name FROM "Customers" WHERE "Id" != 1 ORDER BY id` (we don't need admin to have a role).

and then update `CHANGEME` values in `publish.py` and run `python publish.py`. This will raise an SNS notification for each id/name value in the json array. This will be added to a queue and processed by IIIF-Auth-v2 Management as if a new customer was created.

Customers that already have a "clickthrough" access-service are skipped, so it is safe to rerun.
