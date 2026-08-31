/*
	only for Cloudflare Worker
*/

export default {
    async fetch(request, env) {
        const url = new URL(request.url);

        let response;
        let contentEncoding = null;

        const acceptEncoding =
            request.headers.get("Accept-Encoding") || "";

        const supportsBr =
            /(?:^|[,\s])br(?:\s*;|\s|,|$)/i.test(acceptEncoding);

        const supportsGzip =
            /(?:^|[,\s])gzip(?:\s*;|\s|,|$)/i.test(acceptEncoding);

        /*
         * Avalonia / .NET WASM:
         *
         * /foo.wasm
         *      ↓
         * /foo.wasm.br
         *
         * /foo.wasm
         *      ↓
         * /foo.wasm.gz
         *
         * /foo.wasm
         *      ↓
         * /foo.wasm
         */
        if (url.pathname.endsWith(".wasm")) {
            // Brotli
            if (supportsBr) {
                const brUrl = new URL(url);
                brUrl.pathname += ".br";

                const brRequest = new Request(brUrl, request);
                const brResponse = await env.ASSETS.fetch(brRequest);

                if (brResponse.ok) {
                    response = brResponse;
                    contentEncoding = "br";
                }
            }

            // Gzip fallback
            if (!response && supportsGzip) {
                const gzipUrl = new URL(url);
                gzipUrl.pathname += ".gz";

                const gzipRequest = new Request(gzipUrl, request);
                const gzipResponse = await env.ASSETS.fetch(gzipRequest);

                if (gzipResponse.ok) {
                    response = gzipResponse;
                    contentEncoding = "gzip";
                }
            }

            // Original WASM fallback
            if (!response) {
                response = await env.ASSETS.fetch(request);
            }
        } else {
            // Normal static resources
            response = await env.ASSETS.fetch(request);
        }

        /*
         * Clone headers so we can modify them.
         */
        const headers = new Headers(response.headers);

        headers.set(
            "X-Nyageki-Magic",
            "2857"
        );

        /*
         * Cross-Origin Isolation
         *
         * Required for:
         * - SharedArrayBuffer
         * - WebAssembly Threads
         * - crossOriginIsolated
         */
        headers.set(
            "Cross-Origin-Opener-Policy",
            "same-origin"
        );

        headers.set(
            "Cross-Origin-Embedder-Policy",
            "require-corp"
        );

        /*
         * Pre-compressed resource.
         */
        if (contentEncoding) {
            headers.set(
                "Content-Encoding",
                contentEncoding
            );

            headers.set(
                "Vary",
                "Accept-Encoding"
            );

            headers.set(
                "Content-Type",
                "application/wasm"
            );

            /*
             * Tell Workers that the body is already compressed.
             *
             * Otherwise the Worker runtime may try to encode
             * the response again.
             */
            return new Response(response.body, {
                status: response.status,
                statusText: response.statusText,
                headers,
                encodeBody: "manual",
            });
        }

        /*
         * Original / uncompressed resource.
         */
        return new Response(response.body, {
            status: response.status,
            statusText: response.statusText,
            headers,
        });
    },
};