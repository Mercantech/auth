# Statisk Mercantec OAuth demo-SPA (HTML/JS)
FROM nginx:1.27-alpine
COPY external-spa-demo/ /usr/share/nginx/html/
COPY docker/nginx-spa.conf /etc/nginx/conf.d/default.conf
