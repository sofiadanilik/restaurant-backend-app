# Green&Tasty — Restaurant Backend

Backend ресторанного застосунку на **C# / .NET**, розгорнутий в **AWS**.

## AWS Deployment

Інфраструктуру розгорнуто з навчального архіву за допомогою AWS Syndicate.

Використані сервіси:
- AWS Lambda
- Amazon API Gateway
- Amazon DynamoDB
- AWS Amplify

AWS-акаунт верифікований. Завдання Explore AWS зі створення Lambda та AWS Cost Budget виконані.

## Group Pre-order — Owner

### 🟢 Level 1

`GET /guest/groups/{groupReservationId}/sessions`

Отримання списку учасників групового замовлення, їхніх страв, сум кожного учасника та загальної суми.

### 🔵 Level 2

`GET /reservations/{reservationId}/guest-session-id`

Отримання ідентифікатора Owner session.

`POST /group-reservations/{groupReservationId}/submit`

Підтвердження групового замовлення:
- враховуються лише submitted sessions;
- однакові страви об'єднуються за dishId;
- статус order змінюється на SubmittedByCustomer;
- статус group reservation змінюється на Submitted.

### 🟣 Level 3

`DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}`

Видалення сесії учасника з групового замовлення та повернення оновленого списку учасників і загальної суми.

## Testing

Локально пройдено 160 backend-тестів. Усі чотири endpoints опубліковані в AWS API Gateway на стадії `develop`.

## Project Structure

- `dnapp/lambdas/` — backend на C# / .NET
- `Tests/` — автоматизовані тести
- `deployment_resources.json` — опис ресурсів для розгортання
- `AGENTS.md` — інструкції для AI-агента
- `docs/` — технічна документація
