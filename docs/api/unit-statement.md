# Unit Statement / Estado de Cuenta de la Unidad

## [ES] Definición / [EN] Definition

**[ES]** El "Unit Statement" representa el estado de cuenta detallado de una unidad para un período específico. Proporciona una visión completa de la deuda, desglosando el saldo anterior, cargos del mes, pagos y el desglose de componentes de la unidad (depto, bodega, etc.).

**[EN]** The "Unit Statement" represents the detailed account statement of a unit for a specific period. It provides a complete view of the debt, breaking down the previous balance, current charges, payments, and the unit's component breakdown (apartment, storage, etc.).

## Endpoints

### Get Statement / Consultar Estado de Cuenta
**GET** `/api/communities/{communityId}/units/{unitId}/statement/{period}`

### Get My Statement / Consultar Mi Estado de Cuenta (Resident)
**GET** `/api/billing/my-statement/{period}`

## Response Structure / Estructura de Respuesta

| Field / Campo | Type / Tipo | Description / Descripción |
| :--- | :--- | :--- |
| `communityId` | Guid | ID de la comunidad / Community ID |
| `unitId` | Guid | ID de la unidad / Unit ID |
| `unitNumber` | string | Número de unidad (ej. "101") / Unit number |
| `period` | string | Período (YYYY-MM) / Period |
| `previousBalance` | decimal | Saldo anterior pendiente / Previous outstanding balance |
| `currentChargesTotal` | decimal | Total cargos del mes / Total charges for the month |
| `paymentsTotal` | decimal | Total pagos realizados / Total payments made |
| `totalDue` | decimal | Total a pagar / Total due |
| `dueDate` | DateTime | Fecha de vencimiento / Due date |
| `unitTotalCoefficientPct` | decimal | Coeficiente total de la unidad / Total unit coefficient |
| `components` | Array | Desglose de componentes (Depto, Bodega, etc.) / Component breakdown |
| `lines` | Array | Detalle de cobros y pagos / Charges and payments details |

### Component Object
| Field | Type | Description |
| :--- | :--- | :--- |
| `type` | string | Tipo (Department, Parking, Storage) |
| `code` | string | Identificador (ej. "EP-45") |
| `coefficientPct` | decimal | Coeficiente porcentual |
| `isActive` | bool | Si el componente está activo |

## Examples / Ejemplos

### curl
```bash
curl -X GET "https://api.core-edificio.com/api/communities/3fa85f64-5717-4562-b3fc-2c963f66afa6/units/771140b9-80db-e9e6-b947-caca6ee9b605/statement/2024-03" \
     -H "Authorization: Bearer {token}"
```

### JSON Response
```json
{
  "communityId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "unitId": "771140b9-80db-e9e6-b947-caca6ee9b605",
  "unitNumber": "101",
  "period": "2024-03",
  "previousBalance": 1500.00,
  "currentChargesTotal": 25000.50,
  "paymentsTotal": 10000.00,
  "totalDue": 16500.50,
  "dueDate": "2024-03-10T00:00:00Z",
  "unitTotalCoefficientPct": 3.2000,
  "components": [
    {
      "type": "Department",
      "code": "101",
      "coefficientPct": 2.5000,
      "isActive": true
    },
    {
      "type": "Parking",
      "code": "P-45",
      "coefficientPct": 0.5000,
      "isActive": true
    },
    {
      "type": "Storage",
      "code": "B-12",
      "coefficientPct": 0.2000,
      "isActive": true
    }
  ],
  "lines": [
    {
      "type": "Charge",
      "description": "Gasto Común Marzo 2024",
      "amount": 25000.50,
      "date": "2024-03-01T08:00:00Z",
      "period": "2024-03"
    },
    {
      "type": "Payment",
      "description": "Pago Transferencia",
      "amount": 10000.00,
      "date": "2024-03-05T14:30:00Z",
      "period": "2024-03"
    }
  ]
}
```
