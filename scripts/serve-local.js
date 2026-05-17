#!/usr/bin/env node
// serve-local.js — Servidor HTTP local para probar el build WebGL con Brotli.
// Añade Content-Encoding y Content-Type correctos para archivos .br.
//
// Uso:
//   node scripts/serve-local.js
//   Abrir: http://localhost:8000
//
// El servidor simula los headers que CloudFront Function pone en produccion.

const http = require('http');
const fs   = require('fs');
const path = require('path');

const BUILD_DIR = process.argv[2] || '/home/alaslibres/DAM/proyecto/PIANO';
const PORT      = 19000;

const MIME = {
    '.html': 'text/html',
    '.js':   'application/javascript',
    '.css':  'text/css',
    '.png':  'image/png',
    '.ico':  'image/x-icon',
    '.wasm': 'application/wasm',
    '.json': 'application/json',
};

http.createServer((req, res) => {
    let urlPath = req.url.split('?')[0];
    if (urlPath === '/') urlPath = '/index.html';

    const filePath = path.join(BUILD_DIR, urlPath);
    const ext      = path.extname(filePath);

    fs.readFile(filePath, (err, data) => {
        if (err) {
            // Intentar con .br si el archivo base no existe (Unity sirve solo comprimidos)
            fs.readFile(filePath + '.br', (err2, data2) => {
                if (err2) { res.writeHead(404); res.end('Not found: ' + urlPath); return; }
                servirBrotli(res, ext, data2);
            });
            return;
        }

        // Archivo .br pedido directamente (como lo hace Unity index.html)
        if (ext === '.br') {
            const realExt = path.extname(filePath.slice(0, -3));
            servirBrotli(res, realExt, data);
            return;
        }

        const mime = MIME[ext] || 'application/octet-stream';
        res.writeHead(200, {
            'Content-Type': mime,
            'Cross-Origin-Opener-Policy': 'same-origin',
            'Cross-Origin-Embedder-Policy': 'require-corp',
        });
        res.end(data);
    });
}).listen(PORT, () => {
    console.log('[serve-local] Sirviendo: ' + BUILD_DIR);
    console.log('[serve-local] URL: http://localhost:' + PORT);
    console.log('[serve-local] Para probar sin API real: http://localhost:' + PORT + '?dni=TEST&token=FAKE&api=http://localhost:8080');
});

function servirBrotli(res, realExt, data) {
    let contentType = 'application/octet-stream';
    if (realExt === '.wasm') contentType = 'application/wasm';
    else if (realExt === '.js') contentType = 'application/javascript';
    else if (realExt === '.data') contentType = 'application/octet-stream';

    res.writeHead(200, {
        'Content-Type':    contentType,
        'Content-Encoding': 'br',
        'Cross-Origin-Opener-Policy':   'same-origin',
        'Cross-Origin-Embedder-Policy': 'require-corp',
    });
    res.end(data);
}
