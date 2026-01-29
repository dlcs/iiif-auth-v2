-- Create a 'clickthrough' role
-- This is an example config for Customer 7, creating role 'https://api.dlcs.digirati.io/customers/7/roles/clickthrough'
begin transaction ;

WITH
    role_provider_id AS (SELECT gen_random_uuid() AS id),
    access_service_id AS (SELECT gen_random_uuid() AS id),
    -- create a role provder
    insert_role_provider AS (
        INSERT INTO role_providers (id, configuration)
            SELECT id, '{"default": {"config": "Clickthrough"}}'
            FROM role_provider_id
            RETURNING *
    ),
    -- and an access service
    insert_access_service AS (
        INSERT INTO access_services (id, customer, role_provider_id, name, profile, label, heading, note, confirm_label, access_token_error_heading, access_token_error_note, logout_label)
            SELECT access_service_id.id, 7, role_provider_id.id, 'clickthrough', 'active', '{"en":["Sample clickthrough label"]}', '{"en":["Sample clickthrough note"]}', '{"en": ["<p>This is a test of clickthrough via IIIF Authorization Flow 2.0</p>"]}', '{"en":["Accept and Open"]}', null, null, '{"en":["Log out of session"]}'
            FROM role_provider_id, access_service_id
            RETURNING *
    )
-- and finally a role
INSERT INTO roles (id, customer, access_service_id, name)
SELECT 'https://api.dlcs.digirati.io/customers/7/roles/clickthrough', 7, id, 'clickthrough'
FROM access_service_id;

rollback ;
commit ;
