/*
 * Goto Home - one-shot PS4 BinLoader payload.
 *
 * Requests a normal application exit through libSceSystemService.
 * The payload does not kill arbitrary processes, reboot the console, or
 * install a background service.
 */
#include <dlfcn.h>
#include <stdint.h>
#include <stdio.h>

typedef int32_t (*sce_system_service_load_exec_t)(const char *path,
                                                   const char *args[]);

int
main(void)
{
    void *system_service =
        dlopen("/system/common/lib/libSceSystemService.sprx", RTLD_NOW);
    if (system_service == NULL) {
        fprintf(stderr, "Goto Home: unable to load libSceSystemService: %s\n",
                dlerror());
        return 1;
    }

    sce_system_service_load_exec_t load_exec =
        (sce_system_service_load_exec_t)dlsym(system_service,
                                               "sceSystemServiceLoadExec");
    if (load_exec == NULL) {
        fprintf(stderr, "Goto Home: sceSystemServiceLoadExec not found: %s\n",
                dlerror());
        dlclose(system_service);
        return 2;
    }

    /*
     * "exit" asks the system to close the current application and return
     * to the PS4 shell. On success the current title is expected to exit;
     * if the call returns, release our module reference and terminate.
     */
    const int32_t result = load_exec("exit", NULL);
    fprintf(stderr, "Goto Home: exit request returned %d\n", result);
    dlclose(system_service);
    return result == 0 ? 0 : 3;
}
