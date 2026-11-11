# Build the Angular app, then serve it with nginx, which also proxies /api to the API container.
FROM node:24-slim AS build
WORKDIR /client
COPY package.json package-lock.json ./
RUN npm ci
COPY . .
RUN npx ng build --configuration production

FROM nginx:1.27-alpine
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY nginx/security-headers.conf /etc/nginx/snippets/security-headers.conf
COPY --from=build /client/dist/client/browser /usr/share/nginx/html
EXPOSE 80
