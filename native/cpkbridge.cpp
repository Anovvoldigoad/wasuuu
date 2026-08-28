#include <string>
#include <vector>
#include <mutex>
#include <cstring>

// main.cpp from cpk-toolkit is compiled with main renamed to cpk_tool_main.
int cpk_tool_main(int argc, char** argv);
static std::mutex g_lock;

static int run_args(std::vector<std::string> args) {
    std::lock_guard<std::mutex> lock(g_lock);
    std::vector<char*> argv;
    argv.reserve(args.size());
    for (auto& s : args) argv.push_back(s.data());
    try { return cpk_tool_main(static_cast<int>(argv.size()), argv.data()); }
    catch (...) { return 99; }
}

extern "C" __attribute__((visibility("default")))
int nsc_cpk_pack(const char* input_folder, const char* output_cpk, int compress, int mode) {
    if (!input_folder || !output_cpk) return 2;
    std::vector<std::string> args = {"cpk-tool", "-p", input_folder, output_cpk, "-m", std::to_string(mode)};
    if (compress) args.push_back("-c");
    return run_args(std::move(args));
}

extern "C" __attribute__((visibility("default")))
int nsc_cpk_extract(const char* input_cpk, const char* output_folder) {
    if (!input_cpk || !output_folder) return 2;
    return run_args({"cpk-tool", "-e", input_cpk, output_folder});
}
