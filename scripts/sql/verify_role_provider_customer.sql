--- Post-deploy verification for the "role_provider gains customer" migration.
---
--- That migration backfills role_providers.customer from the customer of any associated
--- access_service, using min(customer) where more than one is found, and leaving a default of -1
--- where no access_service references the role_provider.
---
--- This script lists the role_providers where that backfill was ambiguous or impossible, i.e. those
--- that have either:
---   (a) 'multiple-customers'  - access_services for more than one customer, so the customer stored
---                               is only one of the candidates. The other candidates are listed in
---                               candidate_customers and need to be resolved by hand - most likely
---                               by giving each customer its own copy of the role_provider.
---   (b) 'no-customer'         - no associated access_service at all, so customer is the -1 default.
---                               Either assign the correct customer or delete the role_provider.
---
--- No rows returned == nothing to do.
SELECT rp.id                                   AS role_provider_id,
       rp.customer                             AS assigned_customer,
       CASE
           WHEN count(acs.id) = 0 THEN 'no-customer'
           ELSE 'multiple-customers'
       END                                     AS issue,
       array_agg(DISTINCT acs.customer)
           FILTER (WHERE acs.id IS NOT NULL)   AS candidate_customers,
       count(acs.id)                           AS access_service_count,
       rp.configuration
FROM role_providers rp
         LEFT JOIN access_services acs ON acs.role_provider_id = rp.id
GROUP BY rp.id, rp.customer, rp.configuration
HAVING count(DISTINCT acs.customer) <> 1
ORDER BY issue, rp.id;
