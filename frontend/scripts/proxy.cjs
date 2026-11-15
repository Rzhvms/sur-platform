const { readFileSync } = require('node:fs');
const { defineProxy } = require('./proxy-options.cjs');

module.exports = defineProxy(process.env['SUR_API_ORIGIN'], process.env['SUR_API_CA'], readFileSync);
