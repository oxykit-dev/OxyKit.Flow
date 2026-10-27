# Build context: oxykit-mail-web/
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
ARG VITE_BACKEND_URL=http://localhost:5026
ARG VITE_SIGNALR_HUB_URL=http://localhost:5026/hubs
ENV VITE_BACKEND_URL=$VITE_BACKEND_URL \
    VITE_SIGNALR_HUB_URL=$VITE_SIGNALR_HUB_URL
RUN npm run build-only

FROM nginx:1.27-alpine AS final
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
