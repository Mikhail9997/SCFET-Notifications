# SKFET Notification System — Backend API 📢

<div align="center">
  
  ### Распределённая система оповещения для Северо-Кавказского финансово-энергетического техникума
  
  [![.NET 9](https://img.shields.io/badge/.NET_9-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
  [![ASP.NET Core](https://img.shields.io/badge/ASP.NET_Core-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet)
  [![PostgreSQL](https://img.shields.io/badge/PostgreSQL-316192?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
  [![Kafka](https://img.shields.io/badge/Apache_Kafka-231F20?style=for-the-badge&logo=apache-kafka&logoColor=white)](https://kafka.apache.org/)
  [![Redis](https://img.shields.io/badge/Redis-DC382D?style=for-the-badge&logo=redis&logoColor=white)](https://redis.io/)
  [![Docker](https://img.shields.io/badge/Docker-2496ED?style=for-the-badge&logo=docker&logoColor=white)](https://www.docker.com/)
  [![SignalR](https://img.shields.io/badge/SignalR-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/apps/aspnet/signalr)
  
  [![Docker Compose](https://img.shields.io/badge/Docker_Compose-✓-blue)](https://docs.docker.com/compose/)
  
</div>

## Обзор системы

**SKFET Notification System** — это масштабируемая, событийно-ориентированная платформа для оповещения студентов и сотрудников СКФЭТ. Система построена на микросервисной архитектуре с использованием брокера сообщений Kafka и кэширования Redis, что обеспечивает высокую надёжность и производительность.

## Telegram Боты — Триединая система регистрации

Система включает **трёх Telegram-ботов** с чётким разделением ответственности:

### 1. Бот для регистрации студентов
**Назначение:** Самостоятельная регистрация студентов в системе

| Функция | Описание |
|---------|----------|
| **Регистрация** | Студент вводит ФИО, группу, логин, пароль|
| **Валидация** | Проверка на уникальность аккаунта (один студент = один аккаунт) |
| **Статус** | После регистрации заявка уходит в Admin Bot на модерацию |
| **Уведомление** | При успешной модерации студент получает сообщение со ссылкой на APK |

### 2. Бот для регистрации сотрудников
**Назначение:** Регистрация учителей и администраторов

| Функция | Описание |
|---------|----------|
| **Регистрация** | Ввод ФИО, должности, логин, пароль |
| **Лимитирование** | Не более 3 регистраций в день с одного Telegram-аккаунта |
| **Проверка** | Предотвращение спам-регистраций |
| **Модерация** | Заявки направляются в Admin Bot |

### 3. Административный бот
**Назначение:** Управление пользователями и группами

| Функция | Описание |
|---------|----------|
| **Авторизация** | Вход по паролю для доступа к функциям |
| **Модерация заявок** | Просмотр входящих заявок на регистрацию |
| **Управление заявками** | Принять или отклонить заявку студента/сотрудника |
| **Управление группами** | Создание, редактирование, удаление учебных групп |
| **Рассылка APK** | Автоматическая отправка ссылки на Google Drive после подтверждения |

## Технологический стек

### Backend Core
- **.NET 9** — основная платформа
- **ASP.NET Core Web API** — RESTful API
- **Entity Framework Core** — ORM для PostgreSQL
- **SignalR** — real-time уведомления для мобильных клиентов
- **Clean Architecture** — разделение ответственности

### Инфраструктура
- **PostgreSQL 17.5** — основная база данных
- **Apache Kafka** — брокер сообщений для событийной коммуникации
- **Redis** — кэширование
- **Docker & Docker Compose** — контейнеризация и оркестрация

### Интеграции
- **Telegram.Bot** — библиотека для работы с Telegram API
- **Confluent.Kafka** — Kafka клиент для .NET
- **StackExchange.Redis** — Redis клиент

### Бэкап и восстановление
- Автоматический бэкап PostgreSQL — ежедневно в 03:00
- Через запрос api/backup/create
