-- Create an 'appointment:token-based' role, granted by entering an appointment key
-- This is an example config for Customer 7, creating role 'https://api.dlcs.io/customers/7/roles/appointment:token-based'
-- and a sample appointment, key 'a5rf65ne99pa', for assets with string1 starting 'LIDDLE/WW2/HFIU/'
begin transaction ;

WITH
    role_provider_id AS (SELECT gen_random_uuid() AS id),
    access_service_id AS (SELECT gen_random_uuid() AS id),
    -- create a role provider
    insert_role_provider AS (
        INSERT INTO role_providers (id, customer, configuration)
            SELECT id, 7, '{"default": {"config": "Appointment", "gestureTitle": "Use your appointment key", "gestureMessage": "Enter the key from your appointment confirmation"}}'
            FROM role_provider_id
            RETURNING *
    ),
    -- an 'active' access service
    insert_access_service AS (
        INSERT INTO access_services (id, customer, role_provider_id, name, profile, label, heading, note, confirm_label, access_token_error_heading, access_token_error_note, logout_label)
            SELECT access_service_id.id, 7, role_provider_id.id, 'appointment', 'active', '{"en":["Appointment key"]}', '{"en":["Viewing by appointment"]}', '{"en":["Enter the key from your appointment confirmation."]}', '{"en":["Enter key"]}', null, null, '{"en":["End appointment session"]}'
            FROM role_provider_id, access_service_id
            RETURNING *
    ),
    -- a role
    insert_role AS (
        INSERT INTO roles (id, customer, access_service_id, name)
            SELECT 'https://api.dlcs.io/customers/7/roles/appointment:token-based', 7, id, 'appointment:token-based'
            FROM access_service_id
            RETURNING *
    )
-- and finally a sample appointment, with a wide time window
INSERT INTO appointments (id, customer, role_provider_id, token, roles, start, "end", active, name, notes, asset_patterns)
SELECT gen_random_uuid(), 7, id, 'a5rf65ne99pa', 'https://api.dlcs.io/customers/7/roles/appointment:token-based',
       now() - interval '1 day', now() + interval '1 year', true, 'Sample appointment', 'Created by appointment.sql',
       '{"string1": ["LIDDLE/WW2/HFIU/*"]}'
FROM role_provider_id;

rollback ;
commit ;
