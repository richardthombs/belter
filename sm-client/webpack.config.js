const path = require('path')
const HtmlWebpackPlugin = require('html-webpack-plugin')

module.exports = {
	mode: 'development',
	entry: './src/index.ts',
	// Necessary in order to use source maps and debug directly TypeScript files
	devtool: 'source-map',
	module: {
		rules: [
			// Necessary in order to use TypeScript
			{
				test: /\.ts$/,
				use: 'ts-loader',
				exclude: /node_modules/,
			},
		],
	},
	resolve: {
		extensions: ['.ts', '.js'],
	},
	output: {
		filename: 'bundle.js',
		// This line is VERY important for VS Code debugging to attach properly
		// Tamper with it at your own risks
		devtoolModuleFilenameTemplate: '[absolute-resource-path]',
		clean: true,
	},
	plugins: [
		new HtmlWebpackPlugin({
			title: "Belter"
		})
	],
	devServer: {
		//contentBase: path.join(__dirname, 'dist'),
		port: 3000,
		hot: true
	}
}
