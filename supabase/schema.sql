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
  -- Código aleatorio que va en el QR y abre la ficha pública (no se usa el id para que no sea adivinable).
  codigo              text not null unique default substr(replace(gen_random_uuid()::text, '-', ''), 1, 16)
                        check (codigo ~ '^[A-Za-z0-9]{12,40}$'),
  marca               text not null,
  modelo              text not null,
  anio                int not null,
  kilometraje         bigint not null,
  unidad_distancia    text not null default 'km' check (unidad_distancia in ('km', 'mi')),
  precio              numeric(12,2) not null,
  procedencia         text not null check (procedencia in
                        ('Nacional', 'Americano legalizado', 'Americano no legalizado')),
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

-- Permisos explícitos: sin ellos PostgREST responde 403 "permission denied" aunque la política exista
-- (pasa en proyectos donde se quitaron los permisos por defecto). El rol anon no recibe nada.
grant select, insert, update, delete on public.vehiculos to authenticated;
grant usage, select on sequence public.vehiculos_id_seq to authenticated;
revoke all on public.vehiculos from anon;

-- Ficha pública: lo único que puede leer quien escanea un QR (rol anon, sin iniciar sesión).
-- Devuelve un solo auto y solo si se conoce su código; la tabla sigue cerrada para anon.
create or replace function public.ficha_publica(p_codigo text)
returns table (
  marca text, modelo text, anio int, kilometraje bigint, unidad_distancia text, precio numeric,
  procedencia text, motor_litros text, configuracion_motor text, observaciones text, vendido boolean
)
language sql
stable
security definer
set search_path = ''
as $$
  select v.marca, v.modelo, v.anio, v.kilometraje, v.unidad_distancia, v.precio,
         v.procedencia, v.motor_litros, v.configuracion_motor, v.observaciones, v.vendido
  from public.vehiculos v
  where v.codigo = p_codigo
  limit 1;
$$;

revoke all on function public.ficha_publica(text) from public;
grant execute on function public.ficha_publica(text) to anon, authenticated;
