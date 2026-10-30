-- Create an 'ip:reading-room' role, granted to requests from configured IP ranges
-- This is an example config for Customer 7, creating role 'https://api.dlcs.io/customers/7/roles/ip:reading-room'
-- Ranges include localhost (127.0.0.1/32 + ::1/128) to allow local testing, e.g. via Test UI
begin transaction ;

WITH
    role_provider_id AS (SELECT gen_random_uuid() AS id),
    access_service_id AS (SELECT gen_random_uuid() AS id),
    -- create a role provider
    insert_role_provider AS (
        INSERT INTO role_providers (id, customer, configuration)
            SELECT id, 7, '{"default": {"config": "IpRestriction", "ranges": ["127.0.0.1/32", "::1/128", "10.0.0.0/24"], "locationPhrase": "in the Research Centre"}}'
            FROM role_provider_id
            RETURNING *
    ),
    -- and an 'external' access service
    insert_access_service AS (
        INSERT INTO access_services (id, customer, role_provider_id, name, profile, label, heading, note, confirm_label, access_token_error_heading, access_token_error_note, logout_label)
            SELECT access_service_id.id, 7, role_provider_id.id, 'reading-room', 'external', null, '{"en":["Reading room access"]}', '{"en":["This item can be viewed in the Research Centre"]}', null, null, null, null
            FROM role_provider_id, access_service_id
            RETURNING *
    )
-- and finally a role
INSERT INTO roles (id, customer, access_service_id, name)
SELECT 'https://api.dlcs.io/customers/7/roles/ip:reading-room', 7, id, 'ip:reading-room'
FROM access_service_id;

rollback ;
commit ;
