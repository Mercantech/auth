export type AuthClientConfig = {
    baseUrl: string;
    apiKey: string | undefined;
};
export declare function loadConfig(): AuthClientConfig;
export declare class AuthApiError extends Error {
    readonly status: number;
    readonly body: string;
    constructor(message: string, status: number, body: string);
}
export declare function authFetch(config: AuthClientConfig, path: string, init?: RequestInit & {
    requireApiKey?: boolean;
}): Promise<unknown>;
export declare function textResult(data: unknown): {
    content: {
        type: "text";
        text: string;
    }[];
};
export declare function errorResult(err: unknown): {
    content: {
        type: "text";
        text: string;
    }[];
    isError: true;
};
