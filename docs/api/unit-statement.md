# Unit Statement (Estado de Cuenta de la Unidad)

## Definición

El "Unit Statement" representa el estado de cuenta detallado de una unidad para un período específico. Proporciona una visión completa de cuánto debe el residente, desglosando el saldo anterior, los cargos del mes, los abonos realizados (pagos) y el total a pagar.

El objetivo es dar transparencia al residente sobre la composición de su deuda.

## Endpoints

### Consultar Estado de Cuenta (Admin/Committee)

**GET** `/api/communities/{communityId}/units/{unitId}/statement/{period}`

### Consultar Mi Estado de Cuenta (Resident)

**GET** `/api/billing/my-statement/{period}`

## Estructura de Respuesta

```json
{
  "previousBalance": "number", // Saldo pendiente de periodos anteriores
  "currentCharges": "number", // Total de gastos del mes actual + fondo de reserva, etc.
  "payments": "number", // Total de pagos realizados en este periodo
  "totalDue": "number", // previousBalance + currentCharges - payments
  "dueDate": "string (ISO Date)" // Fecha límite de pago
}
```

## Ejemplo

```json
{
  "previousBalance": 1500.00,
  "currentCharges": 25000.50,
  "payments": 10000.00,
  "totalDue": 16500.50,
  "dueDate": "2024-03-10"
}
```
