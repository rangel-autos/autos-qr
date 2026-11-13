-- Generador QR · ficha en línea (13/11/2026)
--
-- El QR ya no lleva el texto del auto sino un enlace a la ficha pública (GitHub Pages, carpeta web/).
-- Esta actualización:
--   1. Da a cada auto un código aleatorio (columna codigo), que es lo que va en el enlace del QR.
--   2. Crea la función ficha_publica(codigo): lo único que puede consultar quien escanea, sin iniciar sesión.
--      Devuelve un solo auto y solo a quien conoce su código. La tabla sigue cerrada para el público.
--
-- Cómo aplicarla: panel de Supabase → SQL Editor → pega y ejecuta este archivo completo, DESPUÉS de
-- 2026-11-13-millaje-y-legalizado.sql. Se puede ejecutar más de una vez sin daño.
--
-- Las instalaciones nuevas no la necesitan: schema.sql ya incluye todo.

begin;

-- El trigger de versión no debe contar esto como una edición de un equipo.
alter table public.vehiculos disable trigger vehiculos_before_update;

-- Al agregar la columna, cada auto existente recibe su propio código.
alter table public.vehiculos
  add column if not exists codigo text not null
    default substr(replace(gen_random_uuid()::text, '-', ''), 1, 16);

alter table public.vehiculos enable trigger vehiculos_before_update;

alter table public.vehiculos drop constraint if exists vehiculos_codigo_check;
alter table public.vehiculos
  add constraint vehiculos_codigo_check check (codigo ~ '^[A-Za-z0-9]{12,40}$');

create unique index if not exists vehiculos_codigo_key on public.vehiculos (codigo);

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

commit;

-- PostgREST guarda en caché columnas y funciones; esto hace que vea las nuevas de inmediato.
notify pgrst, 'reload schema';
