# Statement PDF / Estado de Cuenta en PDF

## Endpoints

### Descargar estado de cuenta PDF (Committee/Admin)
**GET** `/api/communities/{communityId}/billing/units/{unitId}/statement/{period}/pdf`

### Descargar mi estado de cuenta PDF (Resident)
**GET** `/api/communities/{communityId}/billing/my-statement/{period}/pdf`

## Ejemplos curl

### Committee/Admin descarga PDF de una unidad
```bash
curl -X GET "https://api.core-edificio.com/api/communities/3fa85f64-5717-4562-b3fc-2c963f66afa6/billing/units/771140b9-80db-e9e6-b947-caca6ee9b605/statement/2024-03/pdf" \
  -H "Authorization: Bearer {token}" \
  -H "Accept: application/pdf" \
  --output statement-2024-03.pdf
```

### Resident descarga su PDF
```bash
curl -X GET "https://api.core-edificio.com/api/communities/3fa85f64-5717-4562-b3fc-2c963f66afa6/billing/my-statement/2024-03/pdf" \
  -H "Authorization: Bearer {token}" \
  -H "Accept: application/pdf" \
  --output my-statement-2024-03.pdf
```

### Resident intenta descargar otra unidad (espera 403)
```bash
curl -X GET "https://api.core-edificio.com/api/communities/3fa85f64-5717-4562-b3fc-2c963f66afa6/billing/units/771140b9-80db-e9e6-b947-caca6ee9b605/statement/2024-03/pdf" \
  -H "Authorization: Bearer {resident_token}" \
  -H "Accept: application/pdf" \
  -i
```

## Pasos manuales para validar

### 1) Resident descarga su PDF
1. Inicia sesión con un usuario **Resident** y obtiene su `token`.
2. Ejecuta el curl de **mi estado de cuenta**.
3. Verifica que el archivo se descarga y se abre sin errores.

### 2) Committee descarga cualquier unidad de su comunidad
1. Inicia sesión con un usuario **Committee** (o Admin) y obtiene su `token`.
2. Ejecuta el curl de **unidad específica** con `communityId`, `unitId` y `period` válidos.
3. Verifica que el PDF corresponde a la unidad solicitada.

### 3) Resident no puede descargar unidad ajena (403)
1. Inicia sesión con un usuario **Resident** y obtiene su `token`.
2. Ejecuta el curl de **unidad específica** (de otra unidad) con el token de residente.
3. Verifica respuesta **403 Forbidden**.

## Checklist de contenido esperado del PDF

- **Coeficientes**
  - Coeficiente total de la unidad.
  - Coeficientes por componente (Depto, Estacionamiento, Bodega, etc.).
- **Componentes**
  - Lista de componentes con tipo, código/identificador y estado activo.
- **Totales**
  - Saldo anterior.
  - Total cargos del período.
  - Total pagos del período.
  - Total a pagar.
- **Lines (detalle)**
  - Líneas de cargos con descripción, monto y fecha.
  - Líneas de pagos con descripción, monto y fecha.
