-- Generador QR · Rangel Venta de Autos — esquema de Supabase
--
-- Pasos manuales en el panel de Supabase (una sola vez):
--   1. SQL Editor → pega y ejecuta este archivo completo.
--   2. Authentication → Sign In / Providers → Email: desactiva "Allow new users to sign up".
--   3. Authentication → Users → "Add user" → crea la cuenta de la agencia (correo + contraseña),
--      marcando "Auto Confirm User".
--   4. Project Settings → API: copia "Project URL" y la llave "anon public" a supabase.json:
--        { "Url": "https://xxxx.supabase.co", "AnonKey": "eyJ..." }
--
-- Nota: en el plan gratuito el proyecto se pausa tras 7 días sin uso; se reactiva desde el panel.

create table if not exists public.vehiculos (
  id                  bigint generated always as identity primary key,
  marca               text not null,
  modelo              text not null,
  anio                int not null,
  kilometraje         bigint not null,
  precio              numeric(12,2) not null,
  procedencia         text not null check (procedencia in
                        ('Nacional', 'Americano regularizado', 'Americano no regularizado')),
  motor_litros        text not null,
  configuracion_motor text not null,
  observaciones       text not null default '',
  vendido             boolean not null default false,
  creado_en           timestamptz not null default now(),
  actualizado_en      timestamptz not null default now(),
  version             int not null default 1
);

-- Cada cambio sube la versión: así la app detecta si otro equipo modificó el mismo auto.
create or replace function public.vehiculos_before_update()
returns trigger
language plpgsql
as $$
begin
  new.version := old.version + 1;
  new.actualizado_en := now();
  return new;
end;
$$;

drop trigger if exists vehiculos_before_update on public.vehiculos;
create trigger vehiculos_before_update
  before update on public.vehiculos
  for each row execute function public.vehiculos_before_update();

-- Solo una sesión iniciada (la cuenta de la agencia) puede leer y escribir.
alter table public.vehiculos enable row level security;

drop policy if exists "solo usuarios autenticados" on public.vehiculos;
create policy "solo usuarios autenticados" on public.vehiculos
  for all to authenticated using (true) with check (true);
