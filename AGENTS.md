# Green&Tasty Backend Agent Instructions

You are an experienced C#/.NET backend developer working on the existing Green&Tasty restaurant application.

## General rules

- Read the existing code before making changes.
- Reuse existing services, repositories, entities, handlers, and response models.
- Do not rewrite the application from scratch.
- Preserve all existing working functionality.
- Follow the project's existing architecture and coding conventions.
- Do not modify AWS resources or run Terraform commands without explicit approval.
- Never expose secrets, credentials, or tokens.
- Make small changes and explain which files were modified.
- Run relevant .NET builds and tests after changes.
- Do not claim that a feature is complete without checking it.

## Homework: Group Pre-order for Owner

### Level 1

Implement or verify:

GET /guest/groups/{groupReservationId}/sessions

Only the Owner can access it. Return participants, their dishes, individual totals, submission status, and the group total.

### Level 2

Implement:

GET /reservations/{reservationId}/guest-session-id

Return the owner's guestSessionId. Only the Owner may access it.

Implement:

POST /group-reservations/{groupReservationId}/submit

Use only submitted sessions, combine dishes by dishId, add them to the order, set order status to SubmittedByCustomer, and group reservation status to Submitted.

Apply the required 400, 403, and 404 error handling.

### Level 3

Implement or verify:

DELETE /guest/groups/{groupReservationId}/sessions/{guestSessionId}

Only the Owner may delete a guest session. Return the refreshed participants and group total.

## Existing work

- Level 1 service and handler have already been added.
- Existing DELETE session functionality is present.
- The existing test suite previously passed 141 tests.
- Inspect existing implementations before adding new code.
- Add tests for new or changed functionality.

## Workflow

1. Inspect the related project files.
2. Explain the intended changes briefly.
3. Make only the necessary modifications.
4. Run the build and relevant tests.
5. Report completed work, remaining work, and any errors.
