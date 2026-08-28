cmake_minimum_required(VERSION 3.14)
project(nsc_cpkbridge LANGUAGES CXX)
if(NOT DEFINED CPK_TOOLKIT_DIR)
  message(FATAL_ERROR "CPK_TOOLKIT_DIR is required")
endif()
set(CPK_MAIN "${CPK_TOOLKIT_DIR}/src/main.cpp")
add_library(cpkbridge SHARED
  cpkbridge.cpp
  ${CPK_MAIN}
  ${CPK_TOOLKIT_DIR}/src/cpk.cpp
  ${CPK_TOOLKIT_DIR}/src/utf.cpp
  ${CPK_TOOLKIT_DIR}/src/crilayla.cpp
)
set_source_files_properties(${CPK_MAIN} PROPERTIES COMPILE_DEFINITIONS "main=cpk_tool_main")
target_include_directories(cpkbridge PRIVATE ${CPK_TOOLKIT_DIR}/src)
target_compile_features(cpkbridge PRIVATE cxx_std_17)
target_compile_options(cpkbridge PRIVATE -O2 -fvisibility=hidden)
