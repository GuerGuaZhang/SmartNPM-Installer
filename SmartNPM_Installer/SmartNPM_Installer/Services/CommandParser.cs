using System;
using System.Text.RegularExpressions;
using SmartNPM_Installer.Models;

namespace SmartNPM_Installer.Services
{
    /// <summary>
    /// 命令解析服务
    /// </summary>
    public class CommandParser
    {
        // 匹配包名的正则（支持作用域、版本、子命令）
        private static readonly Regex PackageRegex = new Regex(
            @"^(?:npx\s+(?:-y\s+|-p\s+)?|npm\s+(?:install|i)\s+(?:-g\s+|--global\s+)?)?" +  // 前缀
            @"(@[^/\s]+/[^@\s]+|[^@\s]+)" +  // 包名（含作用域）
            @"(?:@([^\s]+))?" +  // 可选版本
            @"(?:\s+(.+))?" +  // 剩余部分作为子命令
            @"$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /// <summary>
        /// 解析用户输入的命令
        /// </summary>
        /// <param name="input">用户输入的原始命令</param>
        /// <returns>解析后的命令对象，如果无法解析则返回null</returns>
        public static ParsedCommand? Parse(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return null;

            input = input.Trim();

            // 检查是否为退出命令
            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                return null; // 特殊处理，由REPL层处理
            }

            // 检查是否为内部命令
            if (input.StartsWith("/"))
            {
                return null; // 特殊处理，由REPL层处理
            }

            // 使用正则匹配
            var match = PackageRegex.Match(input);
            if (!match.Success)
                return null;

            var command = new ParsedCommand
            {
                RawInput = input
            };

            // 识别命令类型
            if (input.StartsWith("npx", StringComparison.OrdinalIgnoreCase))
            {
                command.Source = InstallSource.Npx;
            }
            else if (input.StartsWith("npm", StringComparison.OrdinalIgnoreCase) &&
                     (input.Contains("install") || input.Contains(" i ")))
            {
                command.Source = InstallSource.NpmInstall;
            }
            else
            {
                command.Source = InstallSource.RawPackageName;
            }

            // 提取包名
            var packageName = match.Groups[1].Value;
            if (string.IsNullOrEmpty(packageName))
                return null;

            // 处理作用域包
            if (packageName.StartsWith("@"))
            {
                var slashIndex = packageName.IndexOf('/');
                if (slashIndex > 0)
                {
                    command.IsScoped = true;
                    command.Scope = packageName.Substring(1, slashIndex - 1);
                    command.PackageName = packageName;
                }
                else
                {
                    // 无效的作用域包格式
                    return null;
                }
            }
            else
            {
                command.IsScoped = false;
                command.Scope = null;
                command.PackageName = packageName;
            }

            // 提取版本号
            command.Version = match.Groups[2].Success ? match.Groups[2].Value : null;

            // 提取子命令
            command.SubCommand = match.Groups[3].Success ? match.Groups[3].Value.Trim() : null;

            // 推断二进制名
            command.BinaryName = InferBinaryName(command.PackageName);

            return command;
        }

        /// <summary>
        /// 推断 CLI 二进制名
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <returns>推断的二进制名</returns>
        private static string InferBinaryName(string packageName)
        {
            // 如果是作用域包，二进制名通常是包名的最后一部分
            if (packageName.StartsWith("@"))
            {
                var slashIndex = packageName.IndexOf('/');
                if (slashIndex > 0)
                {
                    return packageName.Substring(slashIndex + 1);
                }
            }

            // 无作用域的包，二进制名通常等于包名
            return packageName;
        }

        /// <summary>
        /// 校验包名是否合法
        /// </summary>
        /// <param name="packageName">包名</param>
        /// <returns>是否合法</returns>
        public static bool IsValidPackageName(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
                return false;

            // npm 包名规则：只允许小写字母、数字、连字符、下划线、点，作用域包以@开头
            var regex = new Regex(@"^(?:@[^/\s]+/)?[a-zA-Z0-9._-]+$");
            return regex.IsMatch(packageName);
        }

        /// <summary>
        /// 构造安装命令
        /// </summary>
        /// <param name="command">解析后的命令对象</param>
        /// <returns>完整的 npm install 命令</returns>
        public static string BuildInstallCommand(ParsedCommand command)
        {
            var sb = new System.Text.StringBuilder("npm install -g --allow-scripts ");

            if (command.IsScoped)
            {
                sb.Append($"@{command.Scope}/{command.PackageName.Split('/').Last()}");
            }
            else
            {
                sb.Append(command.PackageName);
            }

            if (command.Version != null)
            {
                sb.Append($"@{command.Version}");
            }

            return sb.ToString();
        }
    }
}