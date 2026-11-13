-- Generador QR · actualización del 13/11/2026
--
--   1. Kilometraje o millaje: nueva columna unidad_distancia ('km' o 'mi'). Los autos que ya existen quedan en km.
--   2. Procedencia: "Americano regularizado"    → "Americano legalizado"
--                   "Americano no regularizado" → "Americano no legalizado"
--
-- Cómo aplicarla: panel de Supabase → SQL Editor → pega y ejecuta este archivo completo (una sola vez;
-- si se ejecuta de nuevo no hace daño). Hazlo justo antes de instalar la versión nueva de la app en los
-- equipos: la versión anterior ya no podrá guardar autos americanos después de este cambio.
--
-- Las instalaciones nuevas no la necesitan: schema.sql ya incluye todo.

begin;

alter table public.vehiculos
  add column if not exists unidad_distancia text not null default 'km';

alter table public.vehiculos drop constraint if exists vehiculos_unidad_distancia_check;
alter table public.vehiculos
  add constraint vehiculos_unidad_distancia_check check (unidad_distancia in ('km', 'mi'));

-- Hay que quitar la regla anterior antes de renombrar los valores.
alter table public.vehiculos drop constraint if exists vehiculos_procedencia_check;

-- El trigger de versión no debe contar esto como una edición de un equipo.
alter table public.vehiculos disable trigger vehiculos_before_update;

update public.vehiculos set procedencia = 'Americano legalizado'
  where procedencia = 'Americano regularizado';
update public.vehiculos set procedencia = 'Americano no legalizado'
  where procedencia = 'Americano no regularizado';

alter table public.vehiculos enable trigger vehiculos_before_update;

alter table public.vehiculos
  add constraint vehiculos_procedencia_check check (procedencia in
    ('Nacional', 'Americano legalizado', 'Americano no legalizado'));

commit;

-- PostgREST guarda en caché las columnas; esto hace que vea la nueva de inmediato.
notify pgrst, 'reload schema';
