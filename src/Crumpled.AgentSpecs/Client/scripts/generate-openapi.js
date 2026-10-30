import fetch from 'node-fetch';
import chalk from 'chalk';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { createClient, defaultPlugins } from '@hey-api/openapi-ts';

// Start notifying user we are generating the TypeScript client
console.log(chalk.green("Generating OpenAPI client..."));

// The API name (the lowercase Constants.ApiName) is the only required argument. The discovery route differs
// between Umbraco 17 (swagger/{name}/swagger.json) and Umbraco 18 (openapi/{name}.json), so it lives in the
// repo-root openapi-route.txt, which is merge=ours between develop/v1 and develop/v2. That keeps this script and
// package.json's generate-client identical on both branch lines.
const apiName = process.argv[2];
const baseUrl = process.argv[3] ?? 'https://localhost:44336';
if (apiName === undefined) {
  console.error(chalk.red(`ERROR: Missing API name`));
  console.error(`Please provide the API name as the first argument found in ${chalk.yellow('package.json')}`);
  console.error(`Example: node generate-openapi.js ${chalk.yellow('myapiname')} [${chalk.yellow('https://localhost:44336')}]`);
  process.exit(1);
}

const routeFile = fileURLToPath(new URL('../../../../openapi-route.txt', import.meta.url));
const route = readFileSync(routeFile, 'utf8').trim().replace('{name}', apiName);
const openApiUrl = `${baseUrl}/umbraco/${route}`;

// Needed to ignore self-signed certificates from running Umbraco on https on localhost
process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';

// Start checking to see if we can connect to the OpenAPI spec
console.log("Ensure your Umbraco instance is running");
console.log(`Fetching OpenAPI definition from ${chalk.yellow(openApiUrl)}`);

fetch(openApiUrl).then(async (response) => {
  if (!response.ok) {
    console.error(chalk.red(`ERROR: OpenAPI spec returned with a non OK (200) response: ${response.status} ${response.statusText}`));
    console.error(`The URL to your Umbraco instance may be wrong or the instance is not running`);
    console.error(`Please verify the base URL (second argument) or the route in ${chalk.yellow('openapi-route.txt')}`);
    process.exit(1);
  }

  console.log(`OpenAPI spec fetched successfully`);
  console.log(`Calling ${chalk.yellow('hey-api')} to generate TypeScript client`);

  await createClient({
    input: openApiUrl,
    output: 'src/api',
    plugins: [
      // Spread defaults so future @hey-api/openapi-ts additions come along automatically,
      // but filter out @hey-api/sdk because we override its responseStyle below.
      ...defaultPlugins.filter((plugin) => (typeof plugin === 'string' ? plugin : plugin.name) !== '@hey-api/sdk'),
      {
        name: '@hey-api/sdk',
        responseStyle: 'fields',
      }
    ],
  });

})
  .catch(error => {
    console.error(`ERROR: Failed to connect to the OpenAPI spec: ${chalk.red(error.message)}`);
    console.error(`The URL to your Umbraco instance may be wrong or the instance is not running`);
    console.error(`Please verify the base URL (second argument) or the route in ${chalk.yellow('openapi-route.txt')}`);
    process.exit(1);
  });
