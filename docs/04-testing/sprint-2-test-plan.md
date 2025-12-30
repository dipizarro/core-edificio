\# Sprint 2 – Test Plan (Gastos Comunes)



\## Objetivo

Validar el flujo completo de gastos comunes:

\- registro de gastos

\- emisión de período

\- prorrateo por coeficientes

\- redondeo y ajuste contable

\- restricciones post-emisión



\## Dataset base

\### Unidades (coeficientes suman 100.00)

101 8.50

102 9.00

103 10.25

104 11.00

201 8.25

202 9.75

203 10.00

204 11.25

301 10.00

302 12.00

Total: 100.00



\### Gastos reales (CLP) para período 2026-01

\- Conserjería: 2400000

\- Aseo: 650000

\- Electricidad áreas comunes: 320450

\- Agua áreas comunes: 180200

\- Mantención ascensores: 280000

\- Bombas / sala bombas: 120000

\- CCTV / monitoreo: 90000

\- Basura / reciclaje: 65000

\- Seguro edificio: 210000

\- Gastos bancarios / admin: 25000

Total esperado: 4340650



\## Escenarios



\### TEST-01 – Happy path: emitir período con prorrateo correcto

\*\*Qué prueba:\*\* cálculo, redondeo a 2 decimales, suma final = total.

\*\*Pasos:\*\*

1\) crear comunidad

2\) crear 10 unidades (coef = 100.00)

3\) crear gastos (total 4.340.650)

4\) emitir período 2026-01

5\) consultar resumen

\*\*Expectativas:\*\*

\- status 200/201 según endpoint

\- BillingPeriod queda Issued

\- existen UnitCharges = 10

\- sum(UnitCharges.Amount) = 4.340.650,00



\### Evidencia (run 2026-01)

\- totalExpenses: 4.340.650

\- chargesTotal: 4.340.650

\- totalCoefficientPct: 100.00

\- unitsCount: 10





\### TEST-02 – Coeficientes inválidos (no suman 100)

\*\*Qué prueba:\*\* validación de regla de negocio.

\*\*Pasos:\*\* crear comunidad con unidades cuya suma ≠ 100 y emitir.

\*\*Expectativas:\*\*

\- 400 BadRequest

\- mensaje: total coefficient must be 100.00



\### TEST-03 – No se pueden agregar gastos a un período emitido

\*\*Qué prueba:\*\* invariantes post-emisión.

\*\*Pasos:\*\* emitir 2026-01 y luego intentar POST expense 2026-01.

\*\*Expectativas:\*\*

\- 409 Conflict

\- mensaje: Period is already issued



\### TEST-04 – No se puede emitir si no hay gastos

\*\*Qué prueba:\*\* emisión sin data.

\*\*Pasos:\*\* crear unidades, no crear gastos, emitir.

\*\*Expectativas:\*\*

\- 400 BadRequest

\- mensaje: No expenses found for this period



\### TEST-05 – Redondeo: diferencia se asigna a mayor coeficiente

\*\*Qué prueba:\*\* regla contable ADR-007.

\*\*Pasos:\*\* usar dataset con total que genera diff (como el base).

\*\*Expectativas:\*\*

\- La suma final calza exacto

\- El ajuste cae en la unidad con mayor coeficiente (o la primera en empate por UnitNumber)



