cmake_minimum_required(VERSION 3.20)
project(RebellionTacticalMath LANGUAGES CXX)

if(NOT CMAKE_CXX_COMPILER_ID MATCHES "^(GNU|Clang|AppleClang)$")
    message(FATAL_ERROR "Build the math library with GCC or Clang.")
endif()

add_library(RebellionTacticalMath SHARED TacticalMath.cpp TacticalFormation.cpp TacticalExports.cpp)
target_compile_features(RebellionTacticalMath PRIVATE cxx_std_20)
target_compile_options(RebellionTacticalMath PRIVATE -O1 -fno-exceptions -fno-rtti)
set_target_properties(RebellionTacticalMath PROPERTIES
    PREFIX ""
    CXX_EXTENSIONS FALSE
    CXX_VISIBILITY_PRESET hidden
    INTERPROCEDURAL_OPTIMIZATION FALSE)
if(NOT WIN32)
    set_target_properties(RebellionTacticalMath PROPERTIES PREFIX "lib")
endif()
if(WIN32)
    target_link_options(RebellionTacticalMath PRIVATE -static-libgcc -static-libstdc++ -Wl,--no-insert-timestamp)
endif()

include(CTest)
if(BUILD_TESTING)
    add_executable(TacticalMathContracts tests/Contracts.cpp)
    target_compile_features(TacticalMathContracts PRIVATE cxx_std_20)
    target_compile_options(TacticalMathContracts PRIVATE -O1)
    target_link_libraries(TacticalMathContracts PRIVATE RebellionTacticalMath)
    if(APPLE)
        set_target_properties(TacticalMathContracts PROPERTIES BUILD_RPATH "@loader_path")
    elseif(UNIX)
        set_target_properties(TacticalMathContracts PROPERTIES BUILD_RPATH "$ORIGIN")
    endif()
    if(WIN32)
        target_link_options(TacticalMathContracts PRIVATE -static-libgcc -static-libstdc++)
    endif()
    add_test(NAME math-contracts COMMAND TacticalMathContracts)
endif()
